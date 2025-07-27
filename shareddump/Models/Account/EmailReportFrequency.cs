namespace SharedDump.Models.Account;

public enum EmailReportFrequency
{
    None = 0,
    Individual = 1,    // Send email for each report individually (default)
    WeeklyDigest = 2   // Combine all reports into one weekly email
}