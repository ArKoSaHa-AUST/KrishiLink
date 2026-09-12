namespace KrishiLink.Models.Entities
{
    public static class ExpenseCategories
    {
        public const string Fuel = "Fuel";
        public const string Labour = "Labour";
        public const string Repair = "Repair";
        public const string Transport = "Transport";
        public const string Fumigation = "Fumigation";
        public const string Utilities = "Utilities";
        public const string Other = "Other";

        public static readonly string[] All = { Fuel, Labour, Repair, Transport, Fumigation, Utilities, Other };
    }
}
