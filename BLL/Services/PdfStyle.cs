using System.Globalization;

namespace KrishiLink.BLL.Services
{
    /// <summary>
    /// Shared palette and formatting helpers for QuestPDF documents (Receipt, P&L, Monthly Statement, etc.).
    /// Uses "BDT" instead of ৳ because bundled PDF fonts lack the Bangladeshi Taka currency glyph.
    /// </summary>
    internal static class PdfStyle
    {
        public const string Brand = "#2d6a4f";
        public const string BrandLight = "#eef7f2";
        public const string Muted = "#5e6e61";
        public const string Border = "#e2e8df";

        public static string Money(decimal v) => "BDT " + v.ToString("N0", CultureInfo.InvariantCulture);
    }
}
