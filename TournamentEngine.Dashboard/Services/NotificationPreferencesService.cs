namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for managing notification and animation preferences
/// Phase 6: Polish & Features - Notification Preferences
/// </summary>
public class NotificationPreferencesService
{
    private readonly ILogger<NotificationPreferencesService> _logger;
    private NotificationPreferencesDto _preferences;
    private readonly string[] _validSpeeds = new[] { "slow", "normal", "fast" };
    private readonly string[] _availableTypes = new[]
    {
        "MatchCompleted",
        "RoundStarted",
        "TournamentFinished",
        "StandingsUpdated",
        "GroupStageCompleted"
    };

    public NotificationPreferencesService(ILogger<NotificationPreferencesService> logger)
    {
        _logger = logger;

        // Initialize with defaults
        _preferences = new NotificationPreferencesDto
        {
            SoundEffectsEnabled = true,
            AnimationsEnabled = true,
            NotificationsEnabled = true,
            SoundVolume = 0.5,
            AnimationSpeed = "normal",
            EnabledNotificationTypes = _availableTypes.ToList(),
            ReducedMotion = false
        };
    }

    public Task<NotificationPreferencesDto> GetPreferencesAsync()
    {
        return Task.FromResult(_preferences);
    }

    public Task SetSoundEffectsEnabledAsync(bool enabled)
    {
        _preferences.SoundEffectsEnabled = enabled;
        _logger.LogInformation("Sound effects {Status}", enabled ? "enabled" : "disabled");
        return Task.CompletedTask;
    }

    public Task SetAnimationsEnabledAsync(bool enabled)
    {
        _preferences.AnimationsEnabled = enabled;
        _logger.LogInformation("Animations {Status}", enabled ? "enabled" : "disabled");
        return Task.CompletedTask;
    }

    public Task SetNotificationsEnabledAsync(bool enabled)
    {
        _preferences.NotificationsEnabled = enabled;
        _logger.LogInformation("Notifications {Status}", enabled ? "enabled" : "disabled");
        return Task.CompletedTask;
    }

    public Task SetSoundVolumeAsync(double volume)
    {
        if (volume < 0 || volume > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0 and 1");
        }

        _preferences.SoundVolume = volume;
        return Task.CompletedTask;
    }

    public Task SetAnimationSpeedAsync(string speed)
    {
        if (!_validSpeeds.Contains(speed))
        {
            throw new ArgumentException($"Invalid animation speed: {speed}. Valid options are: {string.Join(", ", _validSpeeds)}");
        }

        _preferences.AnimationSpeed = speed;
        return Task.CompletedTask;
    }

    public Task<string[]> GetAvailableAnimationSpeedsAsync()
    {
        return Task.FromResult(_validSpeeds);
    }

    public Task SetEnabledNotificationTypesAsync(string[] types)
    {
        _preferences.EnabledNotificationTypes = types.ToList();
        return Task.CompletedTask;
    }

    public Task ResetToDefaultsAsync()
    {
        _preferences = new NotificationPreferencesDto
        {
            SoundEffectsEnabled = true,
            AnimationsEnabled = true,
            NotificationsEnabled = true,
            SoundVolume = 0.5,
            AnimationSpeed = "normal",
            EnabledNotificationTypes = _availableTypes.ToList(),
            ReducedMotion = false
        };
        return Task.CompletedTask;
    }

    public Task<string[]> GetAvailableNotificationTypesAsync()
    {
        return Task.FromResult(_availableTypes);
    }

    public Task<bool> IsNotificationTypeEnabledAsync(string type)
    {
        return Task.FromResult(_preferences.EnabledNotificationTypes.Contains(type));
    }

    public Task SetReducedMotionAsync(bool enabled)
    {
        _preferences.ReducedMotion = enabled;
        
        // If reduced motion is enabled, disable animations
        if (enabled)
        {
            _preferences.AnimationsEnabled = false;
        }
        
        return Task.CompletedTask;
    }
}

/// <summary>
/// Notification preferences DTO
/// </summary>
public class NotificationPreferencesDto
{
    public bool SoundEffectsEnabled { get; set; }
    public bool AnimationsEnabled { get; set; }
    public bool NotificationsEnabled { get; set; }
    public double SoundVolume { get; set; }
    public string AnimationSpeed { get; set; } = "normal";
    public List<string> EnabledNotificationTypes { get; set; } = new();
    public bool ReducedMotion { get; set; }
}
