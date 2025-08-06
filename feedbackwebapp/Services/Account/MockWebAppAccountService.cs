using SharedDump.Models.Account;

namespace FeedbackWebApp.Services.Account;

/// <summary>
/// Mock implementation of account service for development/testing
/// </summary>
public class MockWebAppAccountService : IWebAppAccountService
{
    private readonly ILogger<MockWebAppAccountService> _logger;
    
    // Simulate some user data
    private static readonly UserAccount _mockUser = new()
    {
        UserId = "mock-user-123",
        Tier = AccountTier.Free,
        AnalysesUsed = 3,
        ActiveReports = 1,
        FeedQueriesUsed = 8,
        CreatedAt = DateTime.UtcNow.AddDays(-30),
        LastResetDate = DateTime.UtcNow.AddDays(-5),
        SubscriptionStart = DateTime.UtcNow.AddDays(-30),
        PreferredEmail = "mock@example.com"
    };

    public MockWebAppAccountService(ILogger<MockWebAppAccountService> logger)
    {
        _logger = logger;
    }

    public Task<(UserAccount? account, AccountLimits limits)?> GetUserAccountAndLimitsAsync(TierInfo[]? tierInfo = null)
    {
        _logger.LogInformation("Mock: Getting user account and limits for tier {Tier}", _mockUser.Tier);
        
        AccountLimits limits;

        // If tier info is provided, use it to find limits for the user's tier
        if (tierInfo != null)
        {
            var userTierInfo = tierInfo.FirstOrDefault(t => t.Tier == _mockUser.Tier);
            if (userTierInfo != null)
            {
                limits = new AccountLimits
                {
                    AnalysisLimit = userTierInfo.Limits.AnalysisLimit,
                    ReportLimit = userTierInfo.Limits.ReportLimit,
                    FeedQueryLimit = userTierInfo.Limits.FeedQueryLimit,
                    AnalysisRetentionDays = userTierInfo.Limits.AnalysisRetentionDays
                };
            }
            else
            {
                _logger.LogWarning("User tier {Tier} not found in provided tier info, using fallback", _mockUser.Tier);
                limits = GetFallbackLimits(_mockUser.Tier);
            }
        }
        else
        {
            // Fallback to static limits if tier info not provided
            limits = GetFallbackLimits(_mockUser.Tier);
        }

        return Task.FromResult<(UserAccount? account, AccountLimits limits)?>((account: _mockUser, limits: limits));
    }

    private static AccountLimits GetFallbackLimits(AccountTier tier)
    {
        return tier switch
        {
            AccountTier.Free => new AccountLimits
            {
                AnalysisLimit = 10,
                ReportLimit = 1,
                FeedQueryLimit = 20,
                AnalysisRetentionDays = 30
            },
            AccountTier.Pro => new AccountLimits
            {
                AnalysisLimit = 75,
                ReportLimit = 5,
                FeedQueryLimit = 200,
                AnalysisRetentionDays = 60
            },
            AccountTier.ProPlus => new AccountLimits
            {
                AnalysisLimit = 300,
                ReportLimit = 25,
                FeedQueryLimit = 1000,
                AnalysisRetentionDays = 90
            },
            _ => new AccountLimits
            {
                AnalysisLimit = 10,
                ReportLimit = 1,
                FeedQueryLimit = 20,
                AnalysisRetentionDays = 30
            }
        };
    }

    public Task<TierInfo[]?> GetTierLimitsAsync()
    {
        _logger.LogInformation("Mock: Getting tier limits information");
        
        var tiers = new TierInfo[]
        {
            new()
            {
                Tier = AccountTier.Free,
                Name = "Free",
                Description = "Perfect for getting started with feedback analysis",
                Price = "Free",
                Features = new[]
                {
                    "Basic sentiment analysis",
                    "Limited reports",
                    "Community support"
                },
                Limits = new AccountLimits
                {
                    AnalysisLimit = 10,
                    ReportLimit = 1,
                    FeedQueryLimit = 20,
                    AnalysisRetentionDays = 30
                }
            },
            new()
            {
                Tier = AccountTier.Pro,
                Name = "Pro",
                Description = "Advanced features for professional use",
                Price = "$19/month",
                Features = new[]
                {
                    "Advanced sentiment analysis",
                    "Multiple reports",
                    "Priority support",
                    "Export features",
                    "Custom analysis prompts",
                    "📧 Email notifications",
                    "🐦 Twitter/X access"
                },
                Limits = new AccountLimits
                {
                    AnalysisLimit = 75,
                    ReportLimit = 5,
                    FeedQueryLimit = 200,
                    AnalysisRetentionDays = 60
                }
            },
            new()
            {
                Tier = AccountTier.ProPlus,
                Name = "Pro+",
                Description = "Maximum features for enterprise teams",
                Price = "$49/month",
                Features = new[]
                {
                    "Enterprise sentiment analysis",
                    "Unlimited reports",
                    "24/7 support",
                    "Advanced export features",
                    "Custom analysis prompts",
                    "Team collaboration",
                    "Advanced integrations",
                    "📧 Email notifications",
                    "🐦 Twitter/X access"
                },
                Limits = new AccountLimits
                {
                    AnalysisLimit = 300,
                    ReportLimit = 25,
                    FeedQueryLimit = 1000,
                    AnalysisRetentionDays = 90
                }
            }
        };

        return Task.FromResult<TierInfo[]?>(tiers);
    }

    public Task<ApiKey?> GetApiKeyAsync()
    {
        _logger.LogInformation("Mock: Getting API key");
        
        // Mock API key for testing
        if (_mockUser.Tier >= AccountTier.ProPlus)
        {
            var mockApiKey = new ApiKey
            {
                Key = "ff_mock_api_key_for_testing_only",
                UserId = _mockUser.UserId,
                IsEnabled = false, // Disabled by default
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                Name = "Mock API Key"
            };
            return Task.FromResult<ApiKey?>(mockApiKey);
        }
        
        return Task.FromResult<ApiKey?>(null);
    }

    public Task<ApiKey?> CreateApiKeyAsync(string? name = null)
    {
        _logger.LogInformation("Mock: Creating API key with name '{Name}'", name);
        
        if (_mockUser.Tier < AccountTier.ProPlus)
        {
            _logger.LogWarning("Mock: User does not have permission to create API keys");
            return Task.FromResult<ApiKey?>(null);
        }

        var newApiKey = new ApiKey
        {
            Key = $"ff_mock_created_{DateTime.UtcNow:yyyyMMddHHmmss}",
            UserId = _mockUser.UserId,
            IsEnabled = false, // Disabled by default
            CreatedAt = DateTime.UtcNow,
            Name = name ?? "API Key"
        };
        
        return Task.FromResult<ApiKey?>(newApiKey);
    }

    public Task<bool> DeleteApiKeyAsync()
    {
        _logger.LogInformation("Mock: Deleting API key");
        
        if (_mockUser.Tier < AccountTier.ProPlus)
        {
            _logger.LogWarning("Mock: User does not have permission to delete API keys");
            return Task.FromResult(false);
        }
        
        // Simulate successful deletion
        return Task.FromResult(true);
    }
}
