using System;
using System.ComponentModel.DataAnnotations;
using KrishiLink.Models.Entities;

namespace KrishiLink.Models.ViewModels
{
    public class IntakeLotInput
    {
        [Required]
        public int GodownBookingId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime IntakeDate { get; set; } = DateTime.Today;

        [Required]
        [StringLength(60)]
        public string Crop { get; set; } = string.Empty;

        [StringLength(60)]
        public string? Variety { get; set; }

        [Range(1, 100000, ErrorMessage = "Bags must be between 1 and 100,000.")]
        public int Bags { get; set; } = 1;

        [Range(1, 200, ErrorMessage = "Bag weight must be between 1 and 200 kg.")]
        public decimal BagWeightKg { get; set; } = 50m;

        [Range(0.01, 100000000, ErrorMessage = "Net weight must be greater than 0.")]
        public decimal NetWeightKg { get; set; } = 50m;

        [Range(0, 40, ErrorMessage = "Moisture percentage must be between 0% and 40%.")]
        public decimal? MoisturePercent { get; set; }

        [StringLength(10)]
        public string Grade { get; set; } = IntakeGrades.Ungraded;

        [StringLength(500)]
        public string? Remarks { get; set; }
    }

    public class StorageIntakeLotItemViewModel
    {
        public int Id { get; set; }
        public int GodownBookingId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public DateTime IntakeDate { get; set; }
        public string Crop { get; set; } = string.Empty;
        public string? Variety { get; set; }
        public int Bags { get; set; }
        public decimal BagWeightKg { get; set; }
        public decimal NetWeightKg { get; set; }
        public double NetWeightTons => (double)(NetWeightKg / 1000m);
        public decimal? MoisturePercent { get; set; }
        public string Grade { get; set; } = IntakeGrades.Ungraded;
        public string? Remarks { get; set; }
        public string Status { get; set; } = IntakeLotStatus.Stored;
        public bool IsReleased => Status == IntakeLotStatus.Released;
        public DateTime? ReleasedOn { get; set; }
        public string? ReleasedTo { get; set; }
        public string? ReleaseRemarks { get; set; }
        public DateTime RecordedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string ReceiptPdfUrl { get; set; } = string.Empty;
    }

    public class ReceiptVerificationViewModel
    {
        public bool IsFound { get; set; }
        public bool IsValid => IsFound;
        public string ReceiptNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsReleased => Status == IntakeLotStatus.Released;
        public string GodownName { get; set; } = string.Empty;
        public string StorageType { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Crop { get; set; } = string.Empty;
        public string? Variety { get; set; }
        public int Bags { get; set; }
        public decimal BagWeightKg { get; set; }
        public decimal NetWeightKg { get; set; }
        public double NetWeightTons => (double)(NetWeightKg / 1000m);
        public decimal? MoisturePercent { get; set; }
        public string Grade { get; set; } = string.Empty;
        public string? Remarks { get; set; }
        public DateTime IntakeDate { get; set; }
        public DateTime? ReleasedOn { get; set; }
        public string? ReleasedTo { get; set; }
        public string? ReleaseRemarks { get; set; }
        public string VerificationUrl { get; set; } = string.Empty;
    }

    public record IntakeSummary(decimal StoredKg, decimal ReleasedKg, int LotCount, DateTime? LastIntakeDate)
    {
        public double StoredTons => (double)(StoredKg / 1000m);
        public double ReleasedTons => (double)(ReleasedKg / 1000m);
    }
}
