namespace TournamentEngine.Api.Services;

/// <summary>
/// Service to manage development/testing settings including verification bypass
/// </summary>
public class DevelopmentSettingsService
{
    private bool _bypassVerification = false;
    private readonly object _lock = new object();

    /// <summary>
    /// Enable or disable verification bypass mode
    /// </summary>
    public void SetVerificationBypass(bool enabled)
    {
        lock (_lock)
        {
            _bypassVerification = enabled;
        }
    }

    /// <summary>
    /// Check if verification bypass is currently enabled
    /// </summary>
    public bool IsVerificationBypassed()
    {
        lock (_lock)
        {
            return _bypassVerification;
        }
    }

    /// <summary>
    /// Get current bypass status
    /// </summary>
    public BypassStatusModel GetStatus()
    {
        lock (_lock)
        {
            return new BypassStatusModel
            {
                VerificationBypassed = _bypassVerification,
                Warning = _bypassVerification ? "⚠️ Verification is bypassed - all bots will be accepted!" : null
            };
        }
    }
}

public class BypassStatusModel
{
    public bool VerificationBypassed { get; set; }
    public string? Warning { get; set; }
}
