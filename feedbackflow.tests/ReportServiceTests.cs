using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using FeedbackWebApp.Services;
using SharedDump.Models.Reports;

namespace feedbackflow.tests;

[TestClass]
public class ReportServiceTests
{
    private TestHttpMessageHandler _testHttpMessageHandler;
    private HttpClient _httpClient;
    private IConfiguration _configuration;
    private ReportService _reportService;

    [TestInitialize]
    public void Setup()
    {
        _testHttpMessageHandler = new TestHttpMessageHandler();
        _httpClient = new HttpClient(_testHttpMessageHandler);
        
        _configuration = Substitute.For<IConfiguration>();
        _configuration["FeedbackApi:BaseUrl"].Returns("https://api.test.com");
        _configuration["FeedbackApi:ListReportsCode"].Returns("test-code");

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("DefaultClient").Returns(_httpClient);

        _reportService = new ReportService(httpClientFactory, _configuration);
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

        _testHttpMessageHandler.SetResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseContent)
        });

        // Act
        var result = await _reportService.ListReportsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count());
        
        // Verify the request was made correctly
        var capturedRequest = _testHttpMessageHandler.LastRequest;
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Get, capturedRequest.Method);
        Assert.IsTrue(capturedRequest.RequestUri!.ToString().Contains("ListReports?code=test-code"));
        Assert.IsFalse(capturedRequest.RequestUri!.ToString().Contains("source="));
        Assert.IsFalse(capturedRequest.RequestUri!.ToString().Contains("subsource="));
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

        _testHttpMessageHandler.SetResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseContent)
        });

        // Act
        var result = await _reportService.ListReportsAsync("reddit");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count());
        
        // Verify the request was made correctly
        var capturedRequest = _testHttpMessageHandler.LastRequest;
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Get, capturedRequest.Method);
        Assert.IsTrue(capturedRequest.RequestUri!.ToString().Contains("source=reddit"));
        Assert.IsFalse(capturedRequest.RequestUri!.ToString().Contains("subsource="));
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

        _testHttpMessageHandler.SetResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseContent)
        });

        // Act
        var result = await _reportService.ListReportsAsync("reddit", "dotnet");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count());
        
        // Verify the request was made correctly
        var capturedRequest = _testHttpMessageHandler.LastRequest;
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Get, capturedRequest.Method);
        Assert.IsTrue(capturedRequest.RequestUri!.ToString().Contains("source=reddit"));
        Assert.IsTrue(capturedRequest.RequestUri!.ToString().Contains("subsource=dotnet"));
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

        _testHttpMessageHandler.SetResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseContent)
        });

        // Act
        var result = await _reportService.ListReportsAsync(null, "dotnet");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count());
        
        // Verify the request was made correctly
        var capturedRequest = _testHttpMessageHandler.LastRequest;
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Get, capturedRequest.Method);
        Assert.IsTrue(capturedRequest.RequestUri!.ToString().Contains("subsource=dotnet"));
        Assert.IsTrue(capturedRequest.RequestUri!.ToString().Contains("code=test-code"));
    }

    [TestMethod]
    public async Task ListReportsAsync_WithSpecialCharacters_UrlEncodesParameters()
    {
        // Arrange
        var reports = new ReportModel[0];
        var responseContent = JsonSerializer.Serialize(new { reports });

        _testHttpMessageHandler.SetResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseContent)
        });

        // Act
        var result = await _reportService.ListReportsAsync("test source", "test&subsource");

        // Assert
        Assert.IsNotNull(result);
        
        // Verify the request was made correctly
        var capturedRequest = _testHttpMessageHandler.LastRequest;
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Get, capturedRequest.Method);
        Assert.IsTrue(capturedRequest.RequestUri!.ToString().Contains("source=test source"));
        Assert.IsTrue(capturedRequest.RequestUri!.ToString().Contains("subsource=test%26subsource"));
    }
}

public class TestHttpMessageHandler : HttpMessageHandler
{
    private HttpResponseMessage? _response;
    public HttpRequestMessage? LastRequest { get; private set; }

    public void SetResponse(HttpResponseMessage response)
    {
        _response = response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(_response ?? new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}