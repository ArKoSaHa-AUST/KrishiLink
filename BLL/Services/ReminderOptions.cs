namespace KrishiLink.BLL.Services
{
    public class ReminderOptions
    {
        public const string SectionName = "Reminders";

        public int PollMinutes { get; set; } = 60;
        public int StorageEndingDays { get; set; } = 3;
        public int UnpaidNudgeHours { get; set; } = 48;
        public int StalePendingHours { get; set; } = 24;
        public int CompletionOverdueDays { get; set; } = 2;
    }
}
