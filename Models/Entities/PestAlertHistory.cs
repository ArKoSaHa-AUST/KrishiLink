namespace KrishiLink.Models.Entities
{
    /// <summary>
    /// One pest/disease rule firing in one district on one day (ADV-06). Unique per (district, rule, day), so recording is
    /// idempotent. Kept so farmers can see recent risk and agronomists can tune thresholds against feedback.
    /// </summary>
    public class PestAlertHistory
    {
        public int Id { get; set; }
        public string District { get; set; } = string.Empty;
        public int RuleId { get; set; }
        public string Severity { get; set; } = string.Empty;
        public double RiskPercentage { get; set; }

        /// <summary>The Bangladesh calendar day the rule fired on.</summary>
        public DateTime TriggeredOn { get; set; }

        /// <summary>Temperature, humidity, rain chance and condition that triggered it.</summary>
        public string WeatherSnapshotJson { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<PestAlertFeedback> Feedback { get; set; } = new();
    }

    /// <summary>"Was this accurate?" — one vote per user per alert day. No admin screen: queried directly when tuning rules.</summary>
    public class PestAlertFeedback
    {
        public int Id { get; set; }
        public int PestAlertHistoryId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public bool IsAccurate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public PestAlertHistory? History { get; set; }
        public ApplicationUser? User { get; set; }
    }
}
