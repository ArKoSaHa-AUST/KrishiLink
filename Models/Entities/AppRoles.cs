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
        /// <summary>Accepted and paid by the farmer into platform escrow; only this state can be completed.</summary>
        public const string Paid = "Paid";
        public const string Rejected = "Rejected";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";

        /// <summary>Statuses that occupy dates/capacity and count as upcoming revenue.</summary>
        public static readonly string[] Confirmed = { Accepted, Paid };
    }

    public static class PaymentStatus
    {
        public const string Pending = "Pending";
        public const string Succeeded = "Succeeded";
        public const string Failed = "Failed";
        public const string Refunded = "Refunded";
    }

    public static class PaymentMethods
    {
        public const string BKash = "bKash";
        public const string Nagad = "Nagad";
        public const string Rocket = "Rocket";
        public const string Card = "Card";

        public static readonly string[] All = { BKash, Nagad, Rocket, Card };
    }

    public static class PayoutStatus
    {
        public const string Processing = "Processing";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
    }
}
