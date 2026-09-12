using System;

namespace KrishiLink.Models.Entities
{
    public static class IntakeLotStatus
    {
        public const string Stored = "Stored";
        public const string Released = "Released";
    }

    public static class IntakeGrades
    {
        public const string A = "A";
        public const string B = "B";
        public const string C = "C";
        public const string Ungraded = "Ungraded";

        public static readonly string[] All = { A, B, C, Ungraded };
    }

    public class StorageIntakeLot
    {
        public int Id { get; set; }

        public int GodownBookingId { get; set; }
        public GodownBooking? Booking { get; set; }

        /// <summary>Official unique receipt reference, assigned upon creation e.g. "KL-WR-2026-00001".</summary>
        public string ReceiptNumber { get; set; } = string.Empty;

        public DateTime IntakeDate { get; set; }
        public string Crop { get; set; } = string.Empty;
        public string? Variety { get; set; }

        public int Bags { get; set; }
        public decimal BagWeightKg { get; set; } = 50m;
        public decimal NetWeightKg { get; set; }

        public decimal? MoisturePercent { get; set; }
        public string Grade { get; set; } = IntakeGrades.Ungraded;
        public string? Remarks { get; set; }

        public string Status { get; set; } = IntakeLotStatus.Stored;
        public DateTime? ReleasedOn { get; set; }
        public string? ReleasedTo { get; set; }
        public string? ReleaseRemarks { get; set; }

        public string RecordedByUserId { get; set; } = string.Empty;
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
