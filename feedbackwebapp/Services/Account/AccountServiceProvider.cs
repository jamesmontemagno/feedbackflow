using FeedbackWebApp.Services.Authentication;

namespace FeedbackWebApp.Services.Account;

/// <summary>
/// Provider interface for account services
/// </summary>
public interface IAccountServiceProvider
{
    IWebAppAccountService GetService();
}

/// <summary>
/// Provider that returns either real or mock account service based on configuration
/// </summary>
public class AccountServiceProvider : IAccountServiceProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthenticationHeaderService _authHeaderService;
    private readonly ILogger<WebAppAccountService> _webAppAccountLogger;
    private readonly ILogger<MockWebAppAccountService> _mockAccountLogger;
    private readonly ILogger<AccountServiceProvider> _providerLogger;
    private readonly IConfiguration _configuration;
    private readonly bool _useMockService;

    public AccountServiceProvider(
        IHttpClientFactory httpClientFactory,
        IAuthenticationHeaderService authHeaderService,
        ILogger<WebAppAccountService> webAppAccountLogger,
        ILogger<MockWebAppAccountService> mockAccountLogger,
        ILogger<AccountServiceProvider> providerLogger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _authHeaderService = authHeaderService;
        _webAppAccountLogger = webAppAccountLogger;
        _mockAccountLogger = mockAccountLogger;
        _providerLogger = providerLogger;
        _configuration = configuration;
        _useMockService = configuration.GetValue<bool>("FeedbackApi:UseMocks", false);
        
        _providerLogger.LogInformation("AccountServiceProvider initialized. UseMocks: {UseMocks}", _useMockService);
    }

    public IWebAppAccountService GetService()
    {
        if (_useMockService)
        {
            _providerLogger.LogDebug("Returning MockWebAppAccountService");
            return new MockWebAppAccountService(_mockAccountLogger);
        }

        _providerLogger.LogDebug("Returning WebAppAccountService");
        return new WebAppAccountService(_httpClientFactory, _authHeaderService, _webAppAccountLogger, _configuration);
    }
}
