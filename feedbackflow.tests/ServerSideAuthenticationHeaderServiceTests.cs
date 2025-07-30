using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FeedbackWebApp.Services.Authentication;
using NSubstitute;

namespace FeedbackFlow.Tests;

[TestClass]
public class ServerSideAuthenticationHeaderServiceTests
{
    [TestMethod]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var configuration = Substitute.For<IConfiguration>();
        var logger = Substitute.For<ILogger<ServerSideAuthenticationHeaderService>>();

        // Act
        var service = new ServerSideAuthenticationHeaderService(
            httpContextAccessor,
            configuration,
            logger);

        // Assert
        Assert.IsNotNull(service);
        Assert.IsInstanceOfType(service, typeof(IAuthenticationHeaderService));
    }

    [TestMethod]
    public void ServerSideAuthenticationHeaderService_HasCorrectInterface()
    {
        // This test verifies the service implements the correct interface
        // which is important for dependency injection
        var serviceType = typeof(ServerSideAuthenticationHeaderService);
        var interfaceType = typeof(IAuthenticationHeaderService);
        
        Assert.IsTrue(interfaceType.IsAssignableFrom(serviceType), 
            "ServerSideAuthenticationHeaderService should implement IAuthenticationHeaderService");
    }
}
