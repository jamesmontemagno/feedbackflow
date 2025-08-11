using SharedDump.Models.Reports;

namespace FeedbackWebApp.Services.Mock;

/// <summary>
/// Mock implementation of admin report config service for development/testing
/// </summary>
public class MockAdminReportConfigService : IAdminReportConfigService
{
    private readonly ILogger<MockAdminReportConfigService> _logger;
    private static readonly List<AdminReportConfigModel> _mockConfigs = new();

    static MockAdminReportConfigService()
    {
        // Initialize with some sample data
        _mockConfigs.AddRange(new[]
        {
            new AdminReportConfigModel
            {
                Id = "mock-config-1",
                Name = "Weekly .NET Reddit Report",
                Type = "reddit",
                Subreddit = "dotnet",
                EmailRecipient = "admin@example.com",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
                LastProcessedAt = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedBy = "mock-admin-user",
                PartitionKey = "AdminReports",
                RowKey = "mock-config-1"
            },
            new AdminReportConfigModel
            {
                Id = "mock-config-2",
                Name = "Microsoft/PowerToys GitHub Issues",
                Type = "github",
                Owner = "microsoft",
                Repo = "PowerToys",
                EmailRecipient = "dev-team@example.com",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
                CreatedBy = "mock-admin-user",
                PartitionKey = "AdminReports",
                RowKey = "mock-config-2"
            },
            new AdminReportConfigModel
            {
                Id = "mock-config-3",
                Name = "Programming Subreddit Weekly",
                Type = "reddit",
                Subreddit = "programming",
                EmailRecipient = "content@example.com",
                IsActive = false,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-15),
                LastProcessedAt = DateTimeOffset.UtcNow.AddDays(-8),
                CreatedBy = "mock-admin-user",
                PartitionKey = "AdminReports",
                RowKey = "mock-config-3"
            }
        });
    }

    public MockAdminReportConfigService(ILogger<MockAdminReportConfigService> logger)
    {
        _logger = logger;
    }

    public async Task<List<AdminReportConfigModel>> GetAllConfigsAsync()
    {
        await Task.Delay(200); // Simulate network delay
        _logger.LogInformation("Mock: Getting all admin report configs - returning {Count} configs", _mockConfigs.Count);
        return _mockConfigs.ToList();
    }

    public async Task<AdminReportConfigModel> CreateConfigAsync(AdminReportConfigModel config)
    {
        await Task.Delay(300); // Simulate network delay
        
        // Generate a new ID and set creation details
        config.Id = $"mock-config-{Guid.NewGuid().ToString()[..8]}";
        config.PartitionKey = "AdminReports";
        config.RowKey = config.Id;
        config.CreatedAt = DateTimeOffset.UtcNow;
        config.CreatedBy = "mock-admin-user";
        
        _mockConfigs.Add(config);
        
        _logger.LogInformation("Mock: Created admin report config '{Name}' with ID {Id}", config.Name, config.Id);
        return config;
    }

    public async Task<AdminReportConfigModel> UpdateConfigAsync(AdminReportConfigModel config)
    {
        await Task.Delay(250); // Simulate network delay
        
        var existingConfig = _mockConfigs.FirstOrDefault(c => c.Id == config.Id);
        if (existingConfig == null)
        {
            throw new InvalidOperationException($"Admin report config with ID {config.Id} not found");
        }

        // Update the existing config
        var index = _mockConfigs.IndexOf(existingConfig);
        _mockConfigs[index] = config;
        
        _logger.LogInformation("Mock: Updated admin report config '{Name}' with ID {Id}", config.Name, config.Id);
        return config;
    }

    public async Task<bool> DeleteConfigAsync(string id)
    {
        await Task.Delay(200); // Simulate network delay
        
        var existingConfig = _mockConfigs.FirstOrDefault(c => c.Id == id);
        if (existingConfig == null)
        {
            _logger.LogWarning("Mock: Attempted to delete non-existent admin report config with ID {Id}", id);
            return false;
        }

        _mockConfigs.Remove(existingConfig);
        _logger.LogInformation("Mock: Deleted admin report config '{Name}' with ID {Id}", existingConfig.Name, id);
        return true;
    }

    public async Task<bool> SendNowAsync(string id)
    {
        await Task.Delay(200);
        var existingConfig = _mockConfigs.FirstOrDefault(c => c.Id == id);
        if (existingConfig == null)
        {
            _logger.LogWarning("Mock: Attempted to send now for non-existent admin report config with ID {Id}", id);
            return false;
        }

        existingConfig.LastProcessedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Mock: Sent admin report now for '{Name}' (ID {Id}) to {Email}", existingConfig.Name, id, existingConfig.EmailRecipient);
        return true;
    }
}
