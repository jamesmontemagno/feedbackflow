using SharedDump.Models.Account;

namespace SharedDump.Utils.Account
{
    public static class AccountTierUtils
    {
        public static string GetTierName(AccountTier tier) => tier switch
        {
            AccountTier.Free => "Free",
            AccountTier.Pro => "Pro",
            AccountTier.ProPlus => "Pro+",
            _ => "Unknown"
        };

        public static string GetTierDescription(AccountTier tier) => tier switch
        {
            AccountTier.Free => "Basic analysis, limited usage, no support.",
            AccountTier.Pro => "Priority processing, increased limits, basic support.",
            AccountTier.ProPlus => "Advanced analytics, email notifications, highest limits.",
            _ => "Unknown tier."
        };
    }
}
