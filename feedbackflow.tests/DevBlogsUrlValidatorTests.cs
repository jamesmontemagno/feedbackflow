using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Utils;

namespace feedbackflow.tests;

[TestClass]
public class DevBlogsUrlValidatorTests
{
    [DataTestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow("https://devblogs.microsoft.com/", false)]
    [DataRow("https://devblogs.microsoft.com/feed", false)]
    [DataRow("https://devblogs.microsoft.com/dotnet/rewriting-nuget-restore-in-dotnet-9", true)]
    [DataRow("https://devblogs.microsoft.com/dotnet/rewriting-nuget-restore-in-dotnet-9/", true)]
    [DataRow("https://devblogs.microsoft.com/dotnet/rewriting-nuget-restore-in-dotnet-9/feed", false)]
    [DataRow("http://devblogs.microsoft.com/dotnet/rewriting-nuget-restore-in-dotnet-9", false)]
    [DataRow("https://devblogs.microsoft.com/other-blog/article", true)]
    [DataRow("https://devblogs.microsoft.com/", false)]
    public void IsValidDevBlogsUrl_WorksAsExpected(string? url, bool expected)
    {
        var result = DevBlogsUrlValidator.IsValidDevBlogsUrl(url);
        Assert.AreEqual(expected, result, $"URL: {url}");
    }
}
