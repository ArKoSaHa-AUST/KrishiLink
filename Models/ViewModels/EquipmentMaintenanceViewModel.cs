using System.ComponentModel.DataAnnotations;

namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// ViewModel for the Equipment Owner's Maintenance & Health Tracker Management page.
    /// </summary>
    public class EquipmentMaintenanceDashboardViewModel
    {
        public int EquipmentId { get; set; }
        public string EquipmentName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string PrimaryImageUrl { get; set; } = string.Empty;
        public string DailyRate { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }

        // KPI Summary Stats
        public decimal TotalMaintenanceCost { get; set; }
        public int TotalServiceLogs => Records.Count;
        public DateTime? LastServicedDate { get; set; }
        public int? DaysSinceLastService { get; set; }
        public string LastServicedStatusText { get; set; } = "No maintenance logged";
        public string HealthBadgeClass { get; set; } = "bg-secondary";

        // Historical Records List
        public List<EquipmentMaintenanceItemViewModel> Records { get; set; } = new();

        // New Record Input Form
        public EquipmentMaintenanceRecordInputModel NewRecord { get; set; } = new();
    }

    /// <summary>
    /// Input model for adding or updating an equipment maintenance record.
    /// </summary>
    public class EquipmentMaintenanceRecordInputModel
    {
        public int EquipmentId { get; set; }

        [Required(ErrorMessage = "Please select the date when the service or repair was performed.")]
        [DataType(DataType.Date)]
        [Display(Name = "Service Date")]
        public DateTime ServiceDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Please specify the type of service performed.")]
        [StringLength(80, ErrorMessage = "Service type cannot exceed 80 characters.")]
        [Display(Name = "Service Type")]
        public string ServiceType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a description of the work done or parts replaced.")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        [Display(Name = "Work Done / Notes")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the maintenance cost in BDT ৳.")]
        [Range(0, 5000000, ErrorMessage = "Cost must be a positive amount.")]
        [Display(Name = "Cost (৳)")]
        public decimal Cost { get; set; }

        [StringLength(150, ErrorMessage = "Workshop / mechanic name cannot exceed 150 characters.")]
        [Display(Name = "Serviced By / Workshop")]
        public string? ServicedBy { get; set; }
    }

    /// <summary>
    /// Item view model for presenting a single maintenance event in tables and public listing timelines.
    /// </summary>
    public class EquipmentMaintenanceItemViewModel
    {
        public int Id { get; set; }
        public int EquipmentId { get; set; }
        public DateTime ServiceDate { get; set; }
        public string ServiceDateFormatted => ServiceDate.ToString("dd MMM yyyy");
        public string ServiceType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public string CostFormatted => $"৳{Cost:N0}";
        public string? ServicedBy { get; set; }
        public int DaysAgo => Math.Max(0, (DateTime.Today - ServiceDate.Date).Days);
        public string DaysAgoText => DaysAgo switch
        {
            0 => "Today",
            1 => "Yesterday",
            _ => $"{DaysAgo} days ago"
        };
        public DateTime CreatedAt { get; set; }
    }
}
