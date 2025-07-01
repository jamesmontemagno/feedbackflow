using System.Net;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using FeedbackWebApp.Services;
using SharedDump.Models.Reports;

namespace FeedbackFlow.Tests;

[TestClass]
public class ReportServiceTests
{
    private TestHttpMessageHandler _testHttpMessageHandler = null!;
    private HttpClient _httpClient = null!;
    private IConfiguration _configuration = null!;
    private IMemoryCache _memoryCache = null!;
    private ReportService _reportService = null!;
    [TestInitialize]
    public void Setup()
    {
        _testHttpMessageHandler = new TestHttpMessageHandler();
        _httpClient = new HttpClient(_testHttpMessageHandler);
        
        _configuration = Substitute.For<IConfiguration>();
        _configuration["FeedbackApi:BaseUrl"].Returns("https://api.test.com");
        _configuration["FeedbackApi:FunctionsKey"].Returns("test-code");

        _memoryCache = Substitute.For<IMemoryCache>();

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("DefaultClient").Returns(_httpClient);

        _reportService = new ReportService(httpClientFactory, _configuration, _memoryCache);
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

    [TestMethod]
    public async Task GetReportAsync_CallTwice_UsesCacheOnSecondCall()
    {
        // Arrange
        var reportId = Guid.NewGuid().ToString();
        var report = new ReportModel { Id = Guid.Parse(reportId), Source = "test", SubSource = "cache" };
        var responseContent = JsonSerializer.Serialize(report);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
        };

        _configuration["FeedbackApi:FunctionsKey"].Returns("test-code");
        _testHttpMessageHandler.SetResponse(response);

        // Act - First call
        var result1 = await _reportService.GetReportAsync(reportId);
        var firstRequestCount = _testHttpMessageHandler.LastRequest != null ? 1 : 0;

        // Act - Second call (should use cache)
        var result2 = await _reportService.GetReportAsync(reportId);

        // Assert
        Assert.IsNotNull(result1);
        Assert.IsNotNull(result2);
        Assert.AreEqual(result1.Id, result2.Id);
        Assert.AreEqual(result1.Source, result2.Source);
        
        // Verify that the cache entry was set
        object? cachedValue;
        _memoryCache.TryGetValue($"report_{reportId}", out cachedValue).Returns(true);
        
        // The HTTP handler should only be called once since the second call uses cache
        Assert.AreEqual(1, firstRequestCount);
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