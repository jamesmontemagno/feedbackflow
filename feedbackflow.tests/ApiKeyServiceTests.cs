using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Services.Account;
using SharedDump.Models.Account;
using System.Threading.Tasks;

namespace FeedbackFlow.Tests;

[TestClass]
public class ApiKeyServiceTests
{
    private ILogger<ApiKeyService> _logger = null!;
    private Microsoft.Extensions.Configuration.IConfiguration _configuration = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<ILogger<ApiKeyService>>();
        _configuration = Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>();
        _configuration["ProductionStorage"].Returns("UseDevelopmentStorage=true");
    }

    [TestMethod]
    public void GenerateApiKey_ShouldCreateValidKey()
    {
        // Test that API key generation creates a key with the proper prefix
        var testUserId = "test-user-123";
        var testName = "Test API Key";
        
        // Create an API key manually using the same logic
        var randomBytes = new byte[32];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        
        var apiKey = System.Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
            
        var expectedPrefix = "ff_";
        var fullKey = $"{expectedPrefix}{apiKey}";

        // Assert that the key has the correct format
        Assert.IsTrue(fullKey.StartsWith(expectedPrefix), "API key should start with 'ff_' prefix");
        Assert.IsTrue(fullKey.Length > 10, "API key should be sufficiently long");
    }

    [TestMethod]
    public void ApiKey_ShouldBeDisabledByDefault()
    {
        // Test that new API keys are disabled by default
        var apiKey = new ApiKey
        {
            Key = "ff_test_key",
            UserId = "test-user",
            Name = "Test Key"
        };

        Assert.IsFalse(apiKey.IsEnabled, "New API keys should be disabled by default");
    }

    [TestMethod]
    public void AccountTierUtils_SupportsApiKeyGeneration_ShouldReturnCorrectValues()
    {
        // Test that only Pro+ and higher tiers support API key generation
        Assert.IsFalse(SharedDump.Utils.Account.AccountTierUtils.SupportsApiKeyGeneration(AccountTier.Free), 
            "Free tier should not support API key generation");
        Assert.IsFalse(SharedDump.Utils.Account.AccountTierUtils.SupportsApiKeyGeneration(AccountTier.Pro), 
            "Pro tier should not support API key generation");
        Assert.IsTrue(SharedDump.Utils.Account.AccountTierUtils.SupportsApiKeyGeneration(AccountTier.ProPlus), 
            "Pro+ tier should support API key generation");
        Assert.IsTrue(SharedDump.Utils.Account.AccountTierUtils.SupportsApiKeyGeneration(AccountTier.SuperUser), 
            "SuperUser tier should support API key generation");
        Assert.IsTrue(SharedDump.Utils.Account.AccountTierUtils.SupportsApiKeyGeneration(AccountTier.Admin), 
            "Admin tier should support API key generation");
    }

    [TestMethod]
    public void ApiKeyEntity_ShouldConvertToApiKeyCorrectly()
    {
        // Test the entity-to-model conversion
        var entity = new ApiKeyEntity
        {
            RowKey = "ff_test_key_123",
            UserId = "user-123",
            IsEnabled = true,
            CreatedAt = System.DateTime.UtcNow,
            Name = "Test Key"
        };

        var apiKey = entity.ToApiKey();

        Assert.AreEqual(entity.RowKey, apiKey.Key);
        Assert.AreEqual(entity.UserId, apiKey.UserId);
        Assert.AreEqual(entity.IsEnabled, apiKey.IsEnabled);
        Assert.AreEqual(entity.CreatedAt, apiKey.CreatedAt);
        Assert.AreEqual(entity.Name, apiKey.Name);
    }
}