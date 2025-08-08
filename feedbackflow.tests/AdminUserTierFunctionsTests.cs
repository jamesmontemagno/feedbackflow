using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models.Account;

namespace FeedbackFlow.Tests;

[TestClass]
public class AdminUserTierFunctionsTests
{
    private static readonly AccountTier[] Allowed = new[] { AccountTier.Free, AccountTier.Pro, AccountTier.ProPlus };

    [TestMethod]
    [TestCategory("Account")]
    public void AllowedTiers_CanBeAssigned()
    {
        var user = new UserAccount
        {
            UserId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            SubscriptionStart = DateTime.UtcNow,
            LastResetDate = DateTime.UtcNow
        };

        foreach (var tier in Allowed)
        {
            user.Tier = tier;
            Assert.AreEqual(tier, user.Tier, $"Tier should be set to {tier}");
        }
    }

    [TestMethod]
    [TestCategory("Account")]
    public void DisallowedTiers_NotInAllowedSet()
    {
        Assert.IsFalse(Allowed.Contains(AccountTier.Admin));
        Assert.IsFalse(Allowed.Contains(AccountTier.SuperUser));
    }
}
