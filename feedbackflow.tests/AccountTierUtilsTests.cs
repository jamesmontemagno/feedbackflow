using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models.Account;
using SharedDump.Utils.Account;

namespace FeedbackFlow.Tests;

[TestClass]
public class AccountTierUtilsTests
{
    [TestMethod]
    [TestCategory("Account")]
    public void GetTierName_ShouldReturnCorrectNames()
    {
        Assert.AreEqual("Free", AccountTierUtils.GetTierName(AccountTier.Free));
        Assert.AreEqual("Pro", AccountTierUtils.GetTierName(AccountTier.Pro));
        Assert.AreEqual("Pro+", AccountTierUtils.GetTierName(AccountTier.ProPlus));
        Assert.AreEqual("Super User", AccountTierUtils.GetTierName(AccountTier.SuperUser));
        Assert.AreEqual("Admin", AccountTierUtils.GetTierName(AccountTier.Admin));
    }

    [TestMethod]
    [TestCategory("Account")]
    public void GetTierDescription_ShouldReturnDescriptions()
    {
        var freeDesc = AccountTierUtils.GetTierDescription(AccountTier.Free);
        var proDesc = AccountTierUtils.GetTierDescription(AccountTier.Pro);
        var proPlusDesc = AccountTierUtils.GetTierDescription(AccountTier.ProPlus);
        var superUserDesc = AccountTierUtils.GetTierDescription(AccountTier.SuperUser);
        var adminDesc = AccountTierUtils.GetTierDescription(AccountTier.Admin);

        // Use Assert.Contains for clearer failure messages (MSTEST0037)
        Assert.Contains("Basic analysis", freeDesc);
        Assert.Contains("Priority processing", proDesc);
        Assert.Contains("highest limits", proPlusDesc);
        Assert.Contains("Internal account with unlimited access", superUserDesc);
        Assert.Contains("Internal administrative account with unlimited access", adminDesc);
    }

    [TestMethod]
    [TestCategory("Account")]
    public void SupportsEmailNotifications_ShouldReturnCorrectSupport()
    {
        Assert.IsFalse(AccountTierUtils.SupportsEmailNotifications(AccountTier.Free));
        Assert.IsTrue(AccountTierUtils.SupportsEmailNotifications(AccountTier.Pro));
        Assert.IsTrue(AccountTierUtils.SupportsEmailNotifications(AccountTier.ProPlus));
        Assert.IsTrue(AccountTierUtils.SupportsEmailNotifications(AccountTier.SuperUser));
        Assert.IsTrue(AccountTierUtils.SupportsEmailNotifications(AccountTier.Admin));
    }

    [TestMethod]
    [TestCategory("Account")]
    public void AdminTier_ShouldHaveSameCapabilitiesAsSuperUser()
    {
        // Verify both have admin-like descriptions
        var superUserDesc = AccountTierUtils.GetTierDescription(AccountTier.SuperUser);
        var adminDesc = AccountTierUtils.GetTierDescription(AccountTier.Admin);
        Assert.Contains("Internal account with unlimited access", superUserDesc);
        Assert.Contains("Internal administrative account with unlimited access", adminDesc);
    }

    [TestMethod]
    [TestCategory("Account")]
    public void GetMinimumTierForEmailNotifications_ShouldReturnPro()
    {
        Assert.AreEqual(AccountTier.Pro, AccountTierUtils.GetMinimumTierForEmailNotifications());
    }

    [TestMethod]
    [TestCategory("Account")]
    public void SupportsTwitterAccess_ShouldReturnCorrectSupport()
    {
        Assert.IsFalse(AccountTierUtils.SupportsTwitterAccess(AccountTier.Free));
        Assert.IsTrue(AccountTierUtils.SupportsTwitterAccess(AccountTier.Pro));
        Assert.IsTrue(AccountTierUtils.SupportsTwitterAccess(AccountTier.ProPlus));
        Assert.IsTrue(AccountTierUtils.SupportsTwitterAccess(AccountTier.SuperUser));
        Assert.IsTrue(AccountTierUtils.SupportsTwitterAccess(AccountTier.Admin));
    }

    [TestMethod]
    [TestCategory("Account")]
    public void GetMinimumTierForTwitterAccess_ShouldReturnPro()
    {
        Assert.AreEqual(AccountTier.Pro, AccountTierUtils.GetMinimumTierForTwitterAccess());
    }
}
