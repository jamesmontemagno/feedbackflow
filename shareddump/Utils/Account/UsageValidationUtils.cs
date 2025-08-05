using SharedDump.Models.Account;

namespace SharedDump.Utils.Account
{
    public static class UsageValidationUtils
    {
        public static bool IsWithinLimit(int used, int limit) => used < limit;
    }
}
