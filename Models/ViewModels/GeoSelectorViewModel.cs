namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// Drives the shared cascading Division to District picker (<c>Views/Shared/_GeoSelector.cshtml</c>).
    /// Choosing a division narrows the district list to that division's districts.
    /// </summary>
    public class GeoSelectorViewModel
    {
        /// <summary>Form field name posted for the division; empty to render the district picker alone.</summary>
        public string DivisionField { get; set; } = "Division";

        /// <summary>Form field name posted for the district.</summary>
        public string DistrictField { get; set; } = "District";

        public string? SelectedDivision { get; set; }
        public string? SelectedDistrict { get; set; }

        public string DivisionLabel { get; set; } = "Division";
        public string DistrictLabel { get; set; } = "District";

        /// <summary>Text of the "no filter" option. Null makes both selects required.</summary>
        public string? AnyOptionText { get; set; }

        /// <summary>Submits the enclosing form when either select changes (filter bars).</summary>
        public bool SubmitOnChange { get; set; }

        /// <summary>JavaScript called with the new district value when either select changes.</summary>
        public string? OnDistrictChanged { get; set; }

        /// <summary>Bootstrap select size modifier: "sm", "lg" or empty.</summary>
        public string Size { get; set; } = string.Empty;

        /// <summary>Unique per instance; two pickers on one page must not share it.</summary>
        public string IdPrefix { get; set; } = "geo";

        /// <summary>Overrides the generated element id, for wiring into existing page scripts.</summary>
        public string? DivisionId { get; set; }

        /// <summary>Overrides the generated element id, for wiring into existing page scripts.</summary>
        public string? DistrictId { get; set; }

        /// <summary>Bootstrap grid class applied to each of the two columns.</summary>
        public string ColumnClass { get; set; } = "col-12 col-md-6";

        /// <summary>Hides the labels for compact filter bars.</summary>
        public bool ShowLabels { get; set; } = true;

        public string? HelpText { get; set; }

        public bool Required => AnyOptionText is null;
        public string SizeClass => string.IsNullOrEmpty(Size) ? string.Empty : $"form-select-{Size}";
    }
}
