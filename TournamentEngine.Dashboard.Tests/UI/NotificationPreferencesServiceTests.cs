using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using TournamentEngine.Dashboard.Services;

namespace TournamentEngine.Dashboard.Tests.UI;

public class NotificationPreferencesServiceTests
{
    private Mock<ILogger<NotificationPreferencesService>> _mockLogger;

    public NotificationPreferencesServiceTests()
    {
        _mockLogger = new Mock<ILogger<NotificationPreferencesService>>();
    }

    [Fact]
    public async Task GetPreferences_DefaultsToAllEnabled()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);

        // Act
        var prefs = await service.GetPreferencesAsync();

        // Assert
        prefs.SoundEffectsEnabled.Should().BeTrue();
        prefs.AnimationsEnabled.Should().BeTrue();
        prefs.NotificationsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task SetSoundEffects_UpdatesPreference()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);

        // Act
        await service.SetSoundEffectsEnabledAsync(false);
        var prefs = await service.GetPreferencesAsync();

        // Assert
        prefs.SoundEffectsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SetAnimations_UpdatesPreference()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);

        // Act
        await service.SetAnimationsEnabledAsync(false);
        var prefs = await service.GetPreferencesAsync();

        // Assert
        prefs.AnimationsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SetNotifications_UpdatesPreference()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);

        // Act
        await service.SetNotificationsEnabledAsync(false);
        var prefs = await service.GetPreferencesAsync();

        // Assert
        prefs.NotificationsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SetSoundVolume_UpdatesVolume()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);

        // Act
        await service.SetSoundVolumeAsync(0.7);
        var prefs = await service.GetPreferencesAsync();

        // Assert
        prefs.SoundVolume.Should().Be(0.7);
    }

    [Fact]
    public async Task SetSoundVolume_WithInvalidValue_ThrowsException()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetSoundVolumeAsync(1.5));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetSoundVolumeAsync(-0.1));
    }

    [Fact]
    public async Task SetAnimationSpeed_UpdatesSpeed()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);

        // Act
        await service.SetAnimationSpeedAsync("fast");
        var prefs = await service.GetPreferencesAsync();

        // Assert
        prefs.AnimationSpeed.Should().Be("fast");
    }

    [Fact]
    public async Task SetAnimationSpeed_WithInvalidSpeed_ThrowsException()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetAnimationSpeedAsync("super-fast"));
    }

    [Fact]
    public async Task GetAvailableAnimationSpeeds_ReturnsOptions()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);

        // Act
        var speeds = await service.GetAvailableAnimationSpeedsAsync();

        // Assert
        speeds.Should().Contain(new[] { "slow", "normal", "fast" });
        speeds.Should().HaveCount(3);
    }

    [Fact]
    public async Task SetNotificationTypes_FiltersSpecificEvents()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);
        var enabledTypes = new[] { "MatchCompleted", "TournamentFinished" };

        // Act
        await service.SetEnabledNotificationTypesAsync(enabledTypes);
        var prefs = await service.GetPreferencesAsync();

        // Assert
        prefs.EnabledNotificationTypes.Should().HaveCount(2);
        prefs.EnabledNotificationTypes.Should().Contain("MatchCompleted");
        prefs.EnabledNotificationTypes.Should().Contain("TournamentFinished");
    }

    [Fact]
    public async Task ResetToDefaults_RestoresDefaultPreferences()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);
        await service.SetSoundEffectsEnabledAsync(false);
        await service.SetAnimationsEnabledAsync(false);
        await service.SetSoundVolumeAsync(0.2);

        // Act
        await service.ResetToDefaultsAsync();
        var prefs = await service.GetPreferencesAsync();

        // Assert
        prefs.SoundEffectsEnabled.Should().BeTrue();
        prefs.AnimationsEnabled.Should().BeTrue();
        prefs.SoundVolume.Should().Be(0.5); // Default volume
    }

    [Fact]
    public async Task GetAvailableNotificationTypes_ReturnsAllEventTypes()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);

        // Act
        var types = await service.GetAvailableNotificationTypesAsync();

        // Assert
        types.Should().Contain("MatchCompleted");
        types.Should().Contain("RoundStarted");
        types.Should().Contain("TournamentFinished");
        types.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task IsNotificationTypeEnabled_ChecksSpecificType()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);
        await service.SetEnabledNotificationTypesAsync(new[] { "MatchCompleted" });

        // Act
        var matchEnabled = await service.IsNotificationTypeEnabledAsync("MatchCompleted");
        var roundEnabled = await service.IsNotificationTypeEnabledAsync("RoundStarted");

        // Assert
        matchEnabled.Should().BeTrue();
        roundEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SetReducedMotion_UpdatesAccessibilitySetting()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);

        // Act
        await service.SetReducedMotionAsync(true);
        var prefs = await service.GetPreferencesAsync();

        // Assert
        prefs.ReducedMotion.Should().BeTrue();
    }

    [Fact]
    public async Task SetReducedMotion_DisablesAnimations()
    {
        // Arrange
        var service = new NotificationPreferencesService(_mockLogger.Object);

        // Act
        await service.SetReducedMotionAsync(true);
        var prefs = await service.GetPreferencesAsync();

        // Assert
        prefs.AnimationsEnabled.Should().BeFalse();
    }
}
