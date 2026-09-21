using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KrishiLink.DAL;

public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        value => value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}

public sealed class CalendarDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public CalendarDateTimeConverter() : base(
        value => DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified),
        value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified))
    {
    }
}
