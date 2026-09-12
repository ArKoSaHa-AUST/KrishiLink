using System;
using System.Collections.Generic;

namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// Represents an individual recognition badge earned or trackable by an owner.
    /// </summary>
    public class OwnerBadgeViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CriteriaDescription { get; set; } = string.Empty;
        public string Category { get; set; } = "Trust"; // Trust, Volume, Quality, Reliability, Maintenance, Special
        public string IconClass { get; set; } = "bi-award-fill";
        public string BadgeColorClass { get; set; } = "bg-primary";
        public string GradientClass { get; set; } = "gradient-primary";
        public bool IsEarned { get; set; } = false;
        public int ProgressPercentage { get; set; } = 0; // 0 to 100
        public string ProgressText { get; set; } = string.Empty;
        public int CurrentValue { get; set; } = 0;
        public int TargetValue { get; set; } = 0;
        public int DisplayOrder { get; set; } = 0;
        public DateTime? EarnedAt { get; set; }
    }

    /// <summary>
    /// Represents a ranked owner entry in the leaderboard.
    /// </summary>
    public class OwnerLeaderboardEntryViewModel
    {
        public int Rank { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string BusinessOrFarmName { get; set; } = string.Empty;
        public string DisplayName => !string.IsNullOrWhiteSpace(BusinessOrFarmName) ? BusinessOrFarmName : FullName;
        public string District { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string UserRole { get; set; } = "EquipmentOwner"; // "EquipmentOwner", "GodownOwner", "Both"
        public string RoleDisplay => UserRole == "EquipmentOwner" ? "Equipment Owner" : (UserRole == "GodownOwner" ? "Godown Owner" : "Equipment & Godown Host");

        public bool IsVerified { get; set; } = false;
        public string VerificationStatus { get; set; } = "Unverified";

        // Statistics
        public int CompletedBookingsCount { get; set; } = 0;
        public int ActiveBookingsCount { get; set; } = 0;
        public double AverageRating { get; set; } = 0.0;
        public int ReviewCount { get; set; } = 0;
        public int ActiveListingsCount { get; set; } = 0;
        public int MaintenanceRecordsCount { get; set; } = 0;
        public double TrustScore { get; set; } = 0.0;

        // Earned Badges
        public List<OwnerBadgeViewModel> Badges { get; set; } = new();
        public int BadgesCount => Badges.Count;

        // Visual Presentation
        public string AvatarInitials
        {
            get
            {
                var name = DisplayName.Trim();
                if (string.IsNullOrEmpty(name)) return "KL";
                var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
                return $"{parts[0][0]}{parts[parts.Length - 1][0]}".ToUpperInvariant();
            }
        }

        public string RankMedalClass => Rank switch
        {
            1 => "rank-gold",
            2 => "rank-silver",
            3 => "rank-bronze",
            _ => "rank-default"
        };
    }

    /// <summary>
    /// Master ViewModel for the public Leaderboard page (/Leaderboard).
    /// </summary>
    public class LeaderboardPageViewModel
    {
        // Filter criteria
        public string Category { get; set; } = "All"; // "All", "Equipment", "Godown"
        public string SortBy { get; set; } = "trust"; // "trust", "bookings", "rating"
        public string? District { get; set; }

        // Results
        public List<OwnerLeaderboardEntryViewModel> TopPodium { get; set; } = new(); // Top 3
        public List<OwnerLeaderboardEntryViewModel> RankedList { get; set; } = new(); // Rank 4+
        public List<OwnerLeaderboardEntryViewModel> AllEntries { get; set; } = new();
        public int TotalOwnersCount => AllEntries.Count;

        // Available Filter Metadata
        public List<string> AvailableDistricts { get; set; } = new(OnboardingOptions.Districts);

        // Performance & Caching Status
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public string FormattedGeneratedAt => GeneratedAt.ToString("g");
        public bool IsCached { get; set; } = true;
    }

    /// <summary>
    /// Dashboard widget for owner showing their current rank and badge progress.
    /// </summary>
    public class OwnerBadgeDashboardWidgetViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public int? CurrentRank { get; set; }
        public int TotalRankedOwners { get; set; }
        public string RankText => CurrentRank.HasValue ? $"#{CurrentRank.Value}" : "Unranked";
        public double TrustScore { get; set; }

        public List<OwnerBadgeViewModel> EarnedBadges { get; set; } = new();
        public List<OwnerBadgeViewModel> UnearnedBadges { get; set; } = new();
        public List<OwnerBadgeViewModel> AllBadges { get; set; } = new();

        public int EarnedCount => EarnedBadges.Count;
        public int TotalBadgesCount => AllBadges.Count;
        public int OverallProgressPercentage => TotalBadgesCount > 0 ? (int)Math.Round((double)EarnedCount / TotalBadgesCount * 100) : 0;

        public OwnerBadgeViewModel? NextBadgeToUnlock { get; set; }
    }
}
