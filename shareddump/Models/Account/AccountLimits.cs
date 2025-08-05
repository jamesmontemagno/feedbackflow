namespace SharedDump.Models.Account;

public class AccountLimits
{
    public int AnalysisLimit { get; set; }
    public int ReportLimit { get; set; }
    public int FeedQueryLimit { get; set; }
    public int AnalysisRetentionDays { get; set; }
}

