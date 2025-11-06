using SharedDump.Utils;

namespace FeedbackFlow.Tests;

[TestClass]
public class AnalysisDataHelperTests
{
    [TestMethod]
    public void TruncateUserInput_NullInput_ReturnsNull()
    {
        var result = AnalysisDataHelper.TruncateUserInput(null);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void TruncateUserInput_EmptyInput_ReturnsEmpty()
    {
        var result = AnalysisDataHelper.TruncateUserInput("");
        Assert.AreEqual("", result);
    }

    [TestMethod]
    public void TruncateUserInput_ShortInput_ReturnsUnchanged()
    {
        var input = "This is a short input";
        var result = AnalysisDataHelper.TruncateUserInput(input);
        Assert.AreEqual(input, result);
    }

    [TestMethod]
    public void TruncateUserInput_ExactlyMaxLength_ReturnsUnchanged()
    {
        var input = new string('a', AnalysisDataHelper.MaxUserInputLength);
        var result = AnalysisDataHelper.TruncateUserInput(input);
        Assert.AreEqual(input, result);
    }

    [TestMethod]
    public void TruncateUserInput_ExceedsMaxLength_ReturnsTruncated()
    {
        var input = new string('a', AnalysisDataHelper.MaxUserInputLength + 100);
        var result = AnalysisDataHelper.TruncateUserInput(input);
        
        Assert.IsNotNull(result);
        Assert.AreEqual(AnalysisDataHelper.MaxUserInputLength + 3, result.Length); // +3 for "..."
        Assert.IsTrue(result.EndsWith("..."));
        Assert.AreEqual(new string('a', AnalysisDataHelper.MaxUserInputLength) + "...", result);
    }

    [TestMethod]
    public void TruncateUserInput_LargeManualContent_ReturnsTruncatedWithEllipsis()
    {
        // Simulate a large manual content entry (e.g., 2000 characters)
        var largeContent = new string('x', 2000);
        var result = AnalysisDataHelper.TruncateUserInput(largeContent);
        
        Assert.IsNotNull(result);
        Assert.AreEqual(503, result.Length); // 500 + "..."
        Assert.IsTrue(result.EndsWith("..."));
        Assert.IsTrue(result.StartsWith("xxx"));
    }

    [TestMethod]
    public void TruncateUserInput_PreservesContentUpToMaxLength()
    {
        var input = "The quick brown fox jumps over the lazy dog. " + new string('z', 500);
        var result = AnalysisDataHelper.TruncateUserInput(input);
        
        Assert.IsNotNull(result);
        Assert.IsTrue(result.StartsWith("The quick brown fox"));
        Assert.IsTrue(result.EndsWith("..."));
        Assert.AreEqual(503, result.Length);
    }
}
