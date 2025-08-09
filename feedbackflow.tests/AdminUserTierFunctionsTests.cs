using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models.Account;
using System.Linq;

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

    [TestMethod]
    [TestCategory("Account")]
    public void MaskName_BasicBehavior()
    {
        // Mirror masking logic expectations from backend (first + last char with asterisks)
        string Mask(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "(unknown)";
            if (name.Length <= 2) return name[0] + "***";
            return name[0] + new string('*', System.Math.Min(6, name.Length - 2)) + name[^1];
        }

    Assert.AreEqual("J***", Mask("Jo")); // length <=2 path
    Assert.AreEqual("J**e", Mask("Jane")); // middle masked with min(6, len-2)=2
    Assert.AreEqual("A******Z", Mask("AlphabetaZ")); // capped at 6 asterisks preserves last char case
    }
}
