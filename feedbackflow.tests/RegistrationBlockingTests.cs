using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace FeedbackFlow.Tests;

[TestClass]
public class RegistrationBlockingTests
{
    [TestMethod]
    public void AllowsRegistration_DefaultsToTrue()
    {
        // Arrange
        var configuration = Substitute.For<IConfiguration>();
        configuration.GetValue<bool>("AllowsRegistration", true).Returns(true);

        // Act
        var result = configuration.GetValue<bool>("AllowsRegistration", true);

        // Assert
        Assert.IsTrue(result, "AllowsRegistration should default to true");
        configuration.Received(1).GetValue<bool>("AllowsRegistration", true);
    }

    [TestMethod]
    public void AllowsRegistration_CanBeSetToFalse()
    {
        // Arrange
        var configuration = Substitute.For<IConfiguration>();
        configuration.GetValue<bool>("AllowsRegistration", true).Returns(false);

        // Act
        var result = configuration.GetValue<bool>("AllowsRegistration", true);

        // Assert
        Assert.IsFalse(result, "AllowsRegistration should be able to be set to false");
        configuration.Received(1).GetValue<bool>("AllowsRegistration", true);
    }
}
