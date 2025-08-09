using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace FeedbackFlow.Tests;

[TestClass]
public class RegistrationBlockingTests
{
    [TestMethod]
    public void AllowsRegistration_DefaultsToTrue()
    {
        // Arrange: no explicit key set; expect fallback default true
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var result = configuration.GetValue("AllowsRegistration", true);

        // Assert
        Assert.IsTrue(result, "AllowsRegistration should default to true when not configured");
    }

    [TestMethod]
    public void AllowsRegistration_CanBeSetToFalse()
    {
        // Arrange: explicit false value
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowsRegistration"] = "false"
            })
            .Build();

        // Act
        var result = configuration.GetValue("AllowsRegistration", true);

        // Assert
        Assert.IsFalse(result, "AllowsRegistration should be false when configured false");
    }
}
