using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

using FeedbackFunctions.Services.Storage;
using Microsoft.Extensions.Logging;
using SharedDump.Models;

namespace FeedbackFunctions.Services.Sharing;

public class SharedAnalysisStorageService : ISharedAnalysisStorageService
{
    private readonly FeedbackStorageClients _storage;
    private readonly ITableInitializationService _tableInitializationService;
    private readonly ILogger<SharedAnalysisStorageService> _logger;
    private readonly ConcurrentDictionary<string, string> _sharedAnalysisCache = new();

    public SharedAnalysisStorageService(
        FeedbackStorageClients storage,
        ITableInitializationService tableInitializationService,
        ILogger<SharedAnalysisStorageService> logger)
    {
        _storage = storage;
        _tableInitializationService = tableInitializationService;
        _logger = logger;
    }

    public bool TryGetCachedAnalysis(string id, out string analysisJson)
    {
        return _sharedAnalysisCache.TryGetValue(id, out analysisJson!);
    }

    public void CacheAnalysis(string id, string analysisJson)
    {
        _sharedAnalysisCache[id] = analysisJson;
    }

    public async Task<string> SaveAnalysisAsync(string userId, AnalysisData analysisData, bool isPublic)
    {
        await _tableInitializationService.EnsureSharedAnalysesStorageAsync();

        var id = Guid.NewGuid().ToString();
        var analysisJson = JsonSerializer.Serialize(
            analysisData,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var blobClient = _storage.SharedAnalysesContainer.GetBlobClient($"{id}.json");
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(analysisJson));
        await blobClient.UploadAsync(ms, overwrite: true);

        var sharedAnalysisEntity = new SharedAnalysisEntity(userId, id, analysisData, isPublic);
        await _storage.SharedAnalysesTable.UpsertEntityAsync(sharedAnalysisEntity);

        _sharedAnalysisCache[id] = analysisJson;

        _logger.LogInformation(
            "Successfully saved shared analysis {Id} for user {UserId} (Public: {IsPublic})",
            id,
            userId,
            isPublic);

        return id;
    }

    public void RemoveCachedAnalysis(string id)
    {
        _sharedAnalysisCache.TryRemove(id, out _);
    }
}
