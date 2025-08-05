using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Reports;

namespace FeedbackFunctions.Services.Reports;

public class AdminReportConfigService : IAdminReportConfigService
{
    private readonly TableClient _tableClient;
    private readonly ILogger<AdminReportConfigService> _logger;
    private const string TableName = "AdminReportConfigs";

    public AdminReportConfigService(IConfiguration configuration, ILogger<AdminReportConfigService> logger)
    {
        _logger = logger;
        var connectionString = configuration["ProductionStorage"] ??
                             throw new InvalidOperationException("ProductionStorage connection string not found");
        
        _tableClient = new TableClient(connectionString, TableName);
        
        // Ensure table exists
        _ = Task.Run(async () =>
        {
            try
            {
                await _tableClient.CreateIfNotExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not ensure table {TableName} exists", TableName);
            }
        });
    }

    public async Task<List<AdminReportConfigModel>> GetAllActiveConfigsAsync()
    {
        try
        {
            var configs = new List<AdminReportConfigModel>();
            await foreach (var config in _tableClient.QueryAsync<AdminReportConfigModel>(filter: "IsActive eq true"))
            {
                configs.Add(config);
            }
            return configs.OrderBy(c => c.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active admin report configs");
            return new List<AdminReportConfigModel>();
        }
    }

    public async Task<List<AdminReportConfigModel>> GetAllConfigsAsync()
    {
        try
        {
            var configs = new List<AdminReportConfigModel>();
            await foreach (var config in _tableClient.QueryAsync<AdminReportConfigModel>())
            {
                configs.Add(config);
            }
            return configs.OrderBy(c => c.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all admin report configs");
            return new List<AdminReportConfigModel>();
        }
    }

    public async Task<AdminReportConfigModel?> GetConfigAsync(string id)
    {
        try
        {
            var response = await _tableClient.GetEntityIfExistsAsync<AdminReportConfigModel>("AdminReports", id);
            return response.HasValue ? response.Value : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving admin report config with ID {Id}", id);
            return null;
        }
    }

    public async Task<AdminReportConfigModel> CreateConfigAsync(AdminReportConfigModel config)
    {
        try
        {
            // Generate ID if not provided
            if (string.IsNullOrEmpty(config.Id))
            {
                config.Id = Guid.NewGuid().ToString();
            }

            // Set table entity properties
            config.PartitionKey = "AdminReports";
            config.RowKey = config.Id;
            config.CreatedAt = DateTimeOffset.UtcNow;

            await _tableClient.AddEntityAsync(config);
            _logger.LogInformation("Created admin report config {Id} with name '{Name}'", config.Id, config.Name);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating admin report config");
            throw;
        }
    }

    public async Task<AdminReportConfigModel> UpdateConfigAsync(AdminReportConfigModel config)
    {
        try
        {
            // Ensure partition key and row key are set
            config.PartitionKey = "AdminReports";
            config.RowKey = config.Id;

            await _tableClient.UpdateEntityAsync(config, config.ETag);
            _logger.LogInformation("Updated admin report config {Id}", config.Id);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating admin report config {Id}", config.Id);
            throw;
        }
    }

    public async Task<bool> DeleteConfigAsync(string id)
    {
        try
        {
            await _tableClient.DeleteEntityAsync("AdminReports", id);
            _logger.LogInformation("Deleted admin report config {Id}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting admin report config {Id}", id);
            return false;
        }
    }

    public async Task MarkConfigProcessedAsync(string id, DateTimeOffset processedAt)
    {
        try
        {
            var config = await GetConfigAsync(id);
            if (config != null)
            {
                config.LastProcessedAt = processedAt;
                await UpdateConfigAsync(config);
                _logger.LogDebug("Marked admin report config {Id} as processed at {ProcessedAt}", id, processedAt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error marking admin report config {Id} as processed", id);
        }
    }
}