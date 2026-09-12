namespace KrishiLink.Models.Entities
{
    public static class AppRoles
    {
        public const string Farmer = "Farmer";
        public const string EquipmentOwner = "EquipmentOwner";
        public const string GodownOwner = "GodownOwner";
        public const string Admin = "Admin";

        public static readonly string[] All = { Farmer, EquipmentOwner, GodownOwner, Admin };
    }

    public static class BookingStatus
    {
        public const string Pending = "Pending";
        public const string Accepted = "Accepted";
        public const string Rejected = "Rejected";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
    }
}
