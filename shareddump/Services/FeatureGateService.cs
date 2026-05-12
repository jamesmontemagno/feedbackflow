using Microsoft.Extensions.Configuration;
using SharedDump.Services.Interfaces;

namespace SharedDump.Services;

/// <summary>
/// Service for checking feature availability based on configuration flags.
/// </summary>
public class FeatureGateService : IFeatureGateService
{
    private readonly IConfiguration _configuration;

    public FeatureGateService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public bool IsXEnabled
    {
        get
        {
            var value = _configuration["Features:X:Enabled"];
            return bool.TryParse(value, out var result) && result;
        }
    }
}
