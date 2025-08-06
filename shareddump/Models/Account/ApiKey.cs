using System;

namespace SharedDump.Models.Account;

public class ApiKey
{
    public string Key { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public string Name { get; set; } = "API Key";
}