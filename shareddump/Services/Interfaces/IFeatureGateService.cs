namespace SharedDump.Services.Interfaces;

/// <summary>
/// Service for checking feature availability based on configuration flags.
/// </summary>
public interface IFeatureGateService
{
    /// <summary>
    /// Checks if X/Twitter integration is enabled.
    /// </summary>
    bool IsXEnabled { get; }

    /// <summary>
    /// Checks if X/Twitter should be disabled on the omni search page.
    /// </summary>
    bool IsXDisabledOnSearchPage { get; }
}
