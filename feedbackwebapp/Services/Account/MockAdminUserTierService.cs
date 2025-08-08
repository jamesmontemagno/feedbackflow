using SharedDump.Models.Account;

namespace FeedbackWebApp.Services.Account;

/// <summary>
/// Mock admin user tier service for local development.
/// </summary>
public class MockAdminUserTierService : IAdminUserTierService
{
    private readonly ILogger<MockAdminUserTierService> _logger;
    private readonly List<AdminUserTierInfo> _users = new();

    public MockAdminUserTierService(ILogger<MockAdminUserTierService> logger)
    {
        _logger = logger;
        // Seed some fake users
        for (int i = 0; i < 8; i++)
        {
            var id = Guid.NewGuid().ToString();
            _users.Add(new AdminUserTierInfo
            {
                UserId = id,
                MaskedUserId = id[..4] + "****" + id[^4..],
                Tier = (AccountTier)(i % 3),
                AnalysesUsed = i * 2,
                FeedQueriesUsed = i,
                ActiveReports = i % 2,
                ApiUsed = i * 5,
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            });
        }
    }

    public Task<List<AdminUserTierInfo>?> GetAllUserTiersAsync()
        => Task.FromResult<List<AdminUserTierInfo>?>(_users.ToList());

    public Task<bool> UpdateUserTierAsync(string userId, AccountTier newTier)
    {
        var user = _users.FirstOrDefault(u => u.UserId == userId);
        if (user == null)
            return Task.FromResult(false);
        user.Tier = newTier;
        _logger.LogInformation("Mock updated user {UserId} to {Tier}", userId, newTier);
        return Task.FromResult(true);
    }
}
