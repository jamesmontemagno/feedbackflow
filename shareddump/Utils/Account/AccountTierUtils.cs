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
            AccountTier.SuperUser => "Super User",
            AccountTier.Admin => "Admin",
            _ => "Unknown"
        };

        public static string GetTierDescription(AccountTier tier) => tier switch
        {
            AccountTier.Free => "Basic analysis, limited usage, no support.",
            AccountTier.Pro => "Priority processing, email notifications, increased limits, X access, basic support.",
            AccountTier.ProPlus => "Pro features with highest limits.",
            AccountTier.SuperUser => "Internal account with unlimited access.",
            AccountTier.Admin => "Internal administrative account with unlimited access.",
            _ => "Unknown tier."
        };

        /// <summary>
        /// Checks if the account tier supports email notifications
        /// </summary>
        /// <param name="tier">The account tier to check</param>
        /// <returns>True if the tier supports email notifications, false otherwise</returns>
        public static bool SupportsEmailNotifications(AccountTier tier)
        {
            return tier switch
            {
                AccountTier.Pro => true,
                AccountTier.ProPlus => true,
                AccountTier.SuperUser => true,
                AccountTier.Admin => true,
                _ => false
            };
        }

        /// <summary>
        /// Gets the minimum tier required for email notifications
        /// </summary>
        /// <returns>The minimum account tier that supports email notifications</returns>
        public static AccountTier GetMinimumTierForEmailNotifications()
        {
            return AccountTier.Pro;
        }

        /// <summary>
        /// Checks if the account tier supports Twitter/X access
        /// </summary>
        /// <param name="tier">The account tier to check</param>
        /// <returns>True if the tier supports Twitter/X access, false otherwise</returns>
        public static bool SupportsTwitterAccess(AccountTier tier)
        {
            return tier switch
            {
                AccountTier.Pro => true,
                AccountTier.ProPlus => true,
                AccountTier.SuperUser => true,
                AccountTier.Admin => true,
                _ => false
            };
        }

        /// <summary>
        /// Gets the minimum tier required for Twitter/X access
        /// </summary>
        /// <returns>The minimum account tier that supports Twitter/X access</returns>
        public static AccountTier GetMinimumTierForTwitterAccess()
        {
            return AccountTier.Pro;
        }

        /// <summary>
        /// Checks if the account tier supports API key generation
        /// </summary>
        /// <param name="tier">The account tier to check</param>
        /// <returns>True if the tier supports API key generation, false otherwise</returns>
        public static bool SupportsApiKeyGeneration(AccountTier tier)
        {
            return tier switch
            {
                AccountTier.ProPlus => true,
                AccountTier.SuperUser => true,
                AccountTier.Admin => true,
                _ => false
            };
        }

        /// <summary>
        /// Gets the minimum tier required for API key generation
        /// </summary>
        /// <returns>The minimum account tier that supports API key generation</returns>
        public static AccountTier GetMinimumTierForApiKeyGeneration()
        {
            return AccountTier.ProPlus;
        }
    }
}
