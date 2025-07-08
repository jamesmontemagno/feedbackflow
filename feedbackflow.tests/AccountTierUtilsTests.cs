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
    }

    [TestMethod]
    [TestCategory("Account")]
    public void GetTierDescription_ShouldReturnDescriptions()
    {
        var freeDesc = AccountTierUtils.GetTierDescription(AccountTier.Free);
        var proDesc = AccountTierUtils.GetTierDescription(AccountTier.Pro);
        var proPlusDesc = AccountTierUtils.GetTierDescription(AccountTier.ProPlus);

        Assert.IsTrue(freeDesc.Contains("Basic"));
        Assert.IsTrue(proDesc.Contains("Priority"));
        Assert.IsTrue(proPlusDesc.Contains("Advanced"));
    }
}
