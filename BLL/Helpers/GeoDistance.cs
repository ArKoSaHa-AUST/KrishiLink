using System.Linq.Expressions;

namespace KrishiLink.BLL.Helpers
{
    /// <summary>A search origin and radius: "within <see cref="RadiusKm"/> km of (<see cref="Lat"/>, <see cref="Lng"/>)".</summary>
    public readonly record struct GeoCircle(double Lat, double Lng, double RadiusKm)
    {
        /// <summary>Degrees of latitude/longitude that enclose the circle, for the index-backed prefilter.</summary>
        public (double MinLat, double MaxLat, double MinLng, double MaxLng) BoundingBox()
        {
            var dLat = RadiusKm / GeoDistance.KmPerDegreeLatitude;
            var dLng = RadiusKm / (GeoDistance.KmPerDegreeLatitude * Math.Max(0.01, Math.Cos(Lat * Math.PI / 180)));
            return (Lat - dLat, Lat + dLat, Lng - dLng, Lng + dLng);
        }
    }

    /// <summary>
    /// Proximity search in SQL (DIS-02). A bounding box on the indexed (Latitude, Longitude) columns narrows the rows first;
    /// the exact great-circle (Haversine) distance then filters and sorts them, so paging and ordering stay in PostgreSQL
    /// and no listing is materialized in C# to measure it. Plain SQL maths, so it needs no database extension:
    /// earthdistance's ll_to_earth() is not schema-qualified and breaks expression indexes under restricted search paths.
    /// </summary>
    public static class GeoDistance
    {
        /// <summary>Mean Earth radius (IUGG), km.</summary>
        public const double EarthRadiusKm = 6371.0088;
        public const double KmPerDegreeLatitude = 111.195;

        public const double MinRadiusKm = 1;
        public const double MaxRadiusKm = 300;

        // Bangladesh with a margin: coordinates outside this are not a plausible origin.
        private const double MinLat = 20.0, MaxLat = 27.0, MinLng = 87.5, MaxLng = 93.0;

        public static bool IsInBangladesh(double lat, double lng) =>
            double.IsFinite(lat) && double.IsFinite(lng) && lat is >= MinLat and <= MaxLat && lng is >= MinLng and <= MaxLng;

        /// <summary>
        /// The search circle: the user's own position when it is plausible, else the chosen district's centroid; null when
        /// no radius was asked for or there is no origin. Coordinates are rounded to ~100 m before use.
        /// </summary>
        public static GeoCircle? Circle(double? nearLat, double? nearLng, string? district, double? radiusKm)
        {
            if (radiusKm is not { } r || !double.IsFinite(r) || r <= 0) return null;
            r = Math.Clamp(r, MinRadiusKm, MaxRadiusKm);
            if (nearLat is { } lat && nearLng is { } lng && IsInBangladesh(lat, lng))
                return new GeoCircle(Math.Round(lat, 3), Math.Round(lng, 3), r);
            return DistrictCentre(district) is { } centre ? new GeoCircle(centre.Lat, centre.Lng, r) : null;
        }

        /// <summary>A district's centroid, whichever spelling ("Bogra" / "Bogura") the input or the table uses.</summary>
        public static (double Lat, double Lng)? DistrictCentre(string? district)
        {
            var canonical = BangladeshGeo.Canonical(district);
            if (string.IsNullOrWhiteSpace(canonical)) return null;
            foreach (var (name, centre) in GeoLocationHelper.DistrictCoordinates)
                if (string.Equals(BangladeshGeo.Canonical(name), canonical, StringComparison.OrdinalIgnoreCase)) return centre;
            return null;
        }

        /// <summary>Great-circle distance in km (the same formula the SQL expression uses).</summary>
        public static double Km(double lat1, double lng1, double lat2, double lng2)
        {
            const double rad = Math.PI / 180;
            var a = Math.Pow(Math.Sin((lat2 - lat1) * rad / 2), 2)
                    + Math.Cos(lat1 * rad) * Math.Cos(lat2 * rad) * Math.Pow(Math.Sin((lng2 - lng1) * rad / 2), 2);
            return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        }

        /// <summary>An EF-translatable "distance from the circle's centre" for an entity with nullable coordinates.</summary>
        public static Expression<Func<T, double>> KmFrom<T>(GeoCircle origin, Expression<Func<T, double?>> latitude, Expression<Func<T, double?>> longitude)
        {
            const double rad = Math.PI / 180;
            var lat0 = origin.Lat * rad;
            var lng0 = origin.Lng * rad;
            var cosLat0 = Math.Cos(lat0);
            Expression<Func<double, double, double>> haversine = (la, lo) =>
                2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(
                    Math.Pow(Math.Sin((la * rad - lat0) / 2), 2)
                    + cosLat0 * Math.Cos(la * rad) * Math.Pow(Math.Sin((lo * rad - lng0) / 2), 2))));

            var entity = latitude.Parameters[0];
            var lngBody = new ParameterSwap(longitude.Parameters[0], entity).Visit(longitude.Body)!;
            var body = new ParameterSwap(haversine.Parameters[0], Expression.Property(latitude.Body, "Value")).Visit(haversine.Body)!;
            body = new ParameterSwap(haversine.Parameters[1], Expression.Property(lngBody, "Value")).Visit(body)!;
            return Expression.Lambda<Func<T, double>>(body, entity);
        }

        /// <summary>Rows inside the circle: bounding-box prefilter on the indexed columns, then the exact distance.</summary>
        public static IQueryable<T> Within<T>(IQueryable<T> query, GeoCircle circle, Expression<Func<T, double?>> latitude, Expression<Func<T, double?>> longitude)
        {
            var (minLat, maxLat, minLng, maxLng) = circle.BoundingBox();
            var entity = latitude.Parameters[0];
            var lat = latitude.Body;
            var lng = new ParameterSwap(longitude.Parameters[0], entity).Visit(longitude.Body)!;
            var box = Expression.AndAlso(
                Expression.AndAlso(
                    Expression.GreaterThanOrEqual(lat, Expression.Constant((double?)minLat, typeof(double?))),
                    Expression.LessThanOrEqual(lat, Expression.Constant((double?)maxLat, typeof(double?)))),
                Expression.AndAlso(
                    Expression.GreaterThanOrEqual(lng, Expression.Constant((double?)minLng, typeof(double?))),
                    Expression.LessThanOrEqual(lng, Expression.Constant((double?)maxLng, typeof(double?)))));

            var distance = KmFrom(circle, latitude, longitude);
            var inside = Expression.LessThanOrEqual(new ParameterSwap(distance.Parameters[0], entity).Visit(distance.Body)!, Expression.Constant(circle.RadiusKm));
            return query.Where(Expression.Lambda<Func<T, bool>>(box, entity)).Where(Expression.Lambda<Func<T, bool>>(inside, entity));
        }

        private sealed class ParameterSwap : ExpressionVisitor
        {
            private readonly ParameterExpression _from;
            private readonly Expression _to;

            public ParameterSwap(ParameterExpression from, Expression to)
            {
                _from = from;
                _to = to;
            }

            protected override Expression VisitParameter(ParameterExpression node) => node == _from ? _to : base.VisitParameter(node);
        }
    }
}
