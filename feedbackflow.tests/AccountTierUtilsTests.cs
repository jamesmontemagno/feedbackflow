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

        Assert.IsTrue(freeDesc.Contains("Basic"));
        Assert.IsTrue(proDesc.Contains("Priority"));
        Assert.IsTrue(proPlusDesc.Contains("Advanced"));
        Assert.IsTrue(superUserDesc.Contains("Internal administrative"));
        Assert.IsTrue(adminDesc.Contains("Internal administrative"));
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
    public void AdminTier_ShouldHaveCorrectEnumValue()
    {
        Assert.AreEqual(225, (int)AccountTier.Admin);
    }

    [TestMethod]
    [TestCategory("Account")]
    public void AdminTier_ShouldHaveSameCapabilitiesAsSuperUser()
    {
        // Verify Admin has same email notification support as SuperUser
        Assert.AreEqual(
            AccountTierUtils.SupportsEmailNotifications(AccountTier.SuperUser), 
            AccountTierUtils.SupportsEmailNotifications(AccountTier.Admin));
        
        // Verify both have admin-like descriptions
        var superUserDesc = AccountTierUtils.GetTierDescription(AccountTier.SuperUser);
        var adminDesc = AccountTierUtils.GetTierDescription(AccountTier.Admin);
        Assert.IsTrue(superUserDesc.Contains("Internal administrative"));
        Assert.IsTrue(adminDesc.Contains("Internal administrative"));
    }

    [TestMethod]
    [TestCategory("Account")]
    public void GetMinimumTierForEmailNotifications_ShouldReturnPro()
    {
        Assert.AreEqual(AccountTier.Pro, AccountTierUtils.GetMinimumTierForEmailNotifications());
    }
}
