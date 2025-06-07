using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using FeedbackWebApp.Services;
using SharedDump.Models.Reports;

namespace feedbackflow.tests;

[TestClass]
public class ReportServiceTests
{
    private Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private HttpClient _httpClient;
    private Mock<IConfiguration> _mockConfiguration;
    private ReportService _reportService;

    [TestInitialize]
    public void Setup()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(c => c["FeedbackApi:BaseUrl"]).Returns("https://api.test.com");
        _mockConfiguration.Setup(c => c["FeedbackApi:ListReportsCode"]).Returns("test-code");

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient("DefaultClient")).Returns(_httpClient);

        _reportService = new ReportService(mockHttpClientFactory.Object, _mockConfiguration.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _httpClient?.Dispose();
    }

    [TestMethod]
    public async Task ListReportsAsync_WithoutFilters_ReturnsAllReports()
    {
        // Arrange
        var reports = new[]
        {
            new ReportModel { Id = Guid.NewGuid(), Source = "reddit", SubSource = "dotnet" },
            new ReportModel { Id = Guid.NewGuid(), Source = "reddit", SubSource = "programming" },
            new ReportModel { Id = Guid.NewGuid(), Source = "github", SubSource = "issues" }
        };
        var responseContent = JsonSerializer.Serialize(new { reports });

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        // Act
        var result = await _reportService.ListReportsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count());
        _mockHttpMessageHandler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.ToString().Contains("ListReports?code=test-code") &&
                    !req.RequestUri!.ToString().Contains("source=") &&
                    !req.RequestUri!.ToString().Contains("subsource=")),
                ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task ListReportsAsync_WithSourceFilter_IncludesSourceParameter()
    {
        // Arrange
        var reports = new[]
        {
            new ReportModel { Id = Guid.NewGuid(), Source = "reddit", SubSource = "dotnet" },
            new ReportModel { Id = Guid.NewGuid(), Source = "reddit", SubSource = "programming" }
        };
        var responseContent = JsonSerializer.Serialize(new { reports });

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        // Act
        var result = await _reportService.ListReportsAsync("reddit");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count());
        _mockHttpMessageHandler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.ToString().Contains("source=reddit") &&
                    !req.RequestUri!.ToString().Contains("subsource=")),
                ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task ListReportsAsync_WithBothFilters_IncludesBothParameters()
    {
        // Arrange
        var reports = new[]
        {
            new ReportModel { Id = Guid.NewGuid(), Source = "reddit", SubSource = "dotnet" }
        };
        var responseContent = JsonSerializer.Serialize(new { reports });

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        // Act
        var result = await _reportService.ListReportsAsync("reddit", "dotnet");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count());
        _mockHttpMessageHandler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.ToString().Contains("source=reddit") &&
                    req.RequestUri!.ToString().Contains("subsource=dotnet")),
                ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task ListReportsAsync_WithSubsourceOnly_IncludesSubsourceParameter()
    {
        // Arrange
        var reports = new[]
        {
            new ReportModel { Id = Guid.NewGuid(), Source = "reddit", SubSource = "dotnet" },
            new ReportModel { Id = Guid.NewGuid(), Source = "github", SubSource = "dotnet" }
        };
        var responseContent = JsonSerializer.Serialize(new { reports });

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        // Act
        var result = await _reportService.ListReportsAsync(null, "dotnet");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count());
        _mockHttpMessageHandler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.ToString().Contains("subsource=dotnet") &&
                    req.RequestUri!.ToString().Contains("code=test-code")),
                ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task ListReportsAsync_WithSpecialCharacters_UrlEncodesParameters()
    {
        // Arrange
        var reports = new ReportModel[0];
        var responseContent = JsonSerializer.Serialize(new { reports });

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        // Act
        var result = await _reportService.ListReportsAsync("test source", "test&subsource");

        // Assert
        Assert.IsNotNull(result);
        _mockHttpMessageHandler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.ToString().Contains("source=test source") &&
                    req.RequestUri!.ToString().Contains("subsource=test%26subsource")),
                ItExpr.IsAny<CancellationToken>());
    }
}