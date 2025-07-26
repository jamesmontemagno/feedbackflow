namespace SharedDump.Models.Email;

public class EmailNotificationPreferences
{
    public string? Email { get; set; }
    public bool EmailNotificationsEnabled { get; set; } = false;
    public EmailReportFrequency EmailFrequency { get; set; } = EmailReportFrequency.Immediate;
}

public enum EmailReportFrequency
{
    None = 0,
    Immediate = 1,
    Daily = 2,
    Weekly = 3
}