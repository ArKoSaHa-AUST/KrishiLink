using System.Globalization;
using KrishiLink.Models.Entities;
using Microsoft.Extensions.Localization;

namespace KrishiLink.BLL.Helpers
{
    /// <summary>
    /// Converts and formats the units rural Bangladesh actually uses (REA-03): land in decimal (shotangsho), katha, bigha
    /// and acre; weight in maund (mon). The local unit is shown first with the metric value in brackets, so a farmer can
    /// trust the number without converting in their head. Land is always stored in decimals and weight in kilograms.
    /// </summary>
    public static class UnitFormat
    {
        /// <summary>1 acre = 100 decimals (exact, by definition).</summary>
        public const double DecimalsPerAcre = 100;

        /// <summary>
        /// 1 bigha = 33 decimals, the standard ("sarkari") bigha used across most of Bangladesh. It is a regional unit:
        /// some areas use a local bigha of a different size (older records in parts of the north and south differ). The
        /// platform uses 33 everywhere and says so on the profile page, rather than guessing a district's custom.
        /// </summary>
        public const double DecimalsPerBigha = 33;

        /// <summary>1 bigha = 20 katha, so 1 katha = 1.65 decimals.</summary>
        public const double DecimalsPerKatha = DecimalsPerBigha / 20;

        /// <summary>1 decimal = 1/100 acre = 40.468564224 m².</summary>
        public const double SquareMetresPerDecimal = 40.468564224;

        /// <summary>1 maund (mon) = 40 kg, the trade maund used in Bangladeshi crop markets.</summary>
        public const double KgPerMaund = 40;

        public static IReadOnlyList<LandUnit> LandUnits { get; } = Enum.GetValues<LandUnit>();

        public static double DecimalsPer(LandUnit unit) => unit switch
        {
            LandUnit.Katha => DecimalsPerKatha,
            LandUnit.Bigha => DecimalsPerBigha,
            LandUnit.Acre => DecimalsPerAcre,
            _ => 1
        };

        public static double ToDecimals(double value, LandUnit unit) => value * DecimalsPer(unit);

        public static double FromDecimals(double decimals, LandUnit unit) => decimals / DecimalsPer(unit);

        public static double DecimalsToHectares(double decimals) => decimals * SquareMetresPerDecimal / 10_000;

        public static double KgToMaund(double kg) => kg / KgPerMaund;

        public static double MaundToKg(double maund) => maund * KgPerMaund;

        /// <summary>A stored preference, tolerating unknown or missing values by falling back to decimals.</summary>
        public static LandUnit Parse(string? value) =>
            Enum.TryParse<LandUnit>(value, ignoreCase: true, out var unit) && Enum.IsDefined(unit) ? unit : LandUnit.Decimal;

        public static string UnitName(LandUnit unit, IStringLocalizer l) => unit switch
        {
            LandUnit.Katha => l["katha"],
            LandUnit.Bigha => l["bigha"],
            LandUnit.Acre => l["acre"],
            _ => l["decimal"]
        };

        /// <summary>"1.5 bigha (0.20 ha)"; in decimals, "50 decimal (0.20 ha)".</summary>
        public static string Land(double decimals, LandUnit unit, IStringLocalizer l) =>
            l["{0} {1} ({2} ha)", Number(FromDecimals(decimals, unit)), UnitName(unit, l), DecimalsToHectares(decimals).ToString("0.00", CultureInfo.CurrentCulture)];

        /// <summary>"65 maund (2.6 t)" at a tonne or more, otherwise "12 maund (480 kg)".</summary>
        public static string Weight(double kg, IStringLocalizer l) =>
            kg >= 1000
                ? l["{0} maund ({1} t)", Number(KgToMaund(kg)), (kg / 1000).ToString("0.0", CultureInfo.CurrentCulture)]
                : l["{0} maund ({1} kg)", Number(KgToMaund(kg)), Math.Round(kg).ToString("N0", CultureInfo.CurrentCulture)];

        /// <summary>"65–75 maund (2.6–3.0 t)", or with kg in brackets below a tonne.</summary>
        public static string WeightRange(double minKg, double maxKg, IStringLocalizer l) =>
            maxKg >= 1000
                ? l["{0}–{1} maund ({2}–{3} t)", Number(KgToMaund(minKg)), Number(KgToMaund(maxKg)),
                    (minKg / 1000).ToString("0.0", CultureInfo.CurrentCulture), (maxKg / 1000).ToString("0.0", CultureInfo.CurrentCulture)]
                : l["{0}–{1} maund ({2}–{3} kg)", Number(KgToMaund(minKg)), Number(KgToMaund(maxKg)),
                    Math.Round(minKg).ToString("N0", CultureInfo.CurrentCulture), Math.Round(maxKg).ToString("N0", CultureInfo.CurrentCulture)];

        /// <summary>Whole numbers above 10, one decimal place below — "1.5 bigha" but "65 maund".</summary>
        public static string Number(double value) =>
            Math.Abs(value) >= 10
                ? Math.Round(value).ToString("N0", CultureInfo.CurrentCulture)
                : Math.Round(value, 1).ToString("0.#", CultureInfo.CurrentCulture);
    }
}
