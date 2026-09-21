namespace KrishiLink.BLL.Helpers
{
    internal static class PostgresSearch
    {
        // ILIKE treats these as pattern syntax; searches must still match literal user input.
        public static string Literal(string value) =>
            value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

        public static string Contains(string value) => $"%{Literal(value)}%";
    }
}
