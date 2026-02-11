using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Tests.UI;

public class ThemeServiceTests
{
    private Mock<ILogger<ThemeService>> _mockLogger;

    public ThemeServiceTests()
    {
        _mockLogger = new Mock<ILogger<ThemeService>>();
    }

    [Fact]
    public async Task GetCurrentTheme_DefaultsToLightMode()
    {
        // Arrange
        var service = new ThemeService(_mockLogger.Object);

        // Act
        var result = await service.GetCurrentThemeAsync();

        // Assert
        result.Should().NotBeNull();
        result.Mode.Should().Be("light");
        result.PrimaryColor.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SetTheme_ChangesToDarkMode()
    {
        // Arrange
        var service = new ThemeService(_mockLogger.Object);

        // Act
        await service.SetThemeAsync("dark");
        var result = await service.GetCurrentThemeAsync();

        // Assert
        result.Mode.Should().Be("dark");
    }

    [Fact]
    public async Task SetTheme_WithInvalidMode_ThrowsException()
    {
        // Arrange
        var service = new ThemeService(_mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetThemeAsync("invalid"));
    }

    [Fact]
    public async Task GetAvailableThemes_ReturnsLightAndDarkOptions()
    {
        // Arrange
        var service = new ThemeService(_mockLogger.Object);

        // Act
        var themes = await service.GetAvailableThemesAsync();

        // Assert
        themes.Should().HaveCount(2);
        themes.Should().Contain(t => t.Name == "light");
        themes.Should().Contain(t => t.Name == "dark");
    }

    [Fact]
    public async Task ToggleTheme_SwitchesBetweenLightAndDark()
    {
        // Arrange
        var service = new ThemeService(_mockLogger.Object);
        await service.SetThemeAsync("light");

        // Act
        await service.ToggleThemeAsync();
        var result = await service.GetCurrentThemeAsync();

        // Assert
        result.Mode.Should().Be("dark");
    }

    [Fact]
    public async Task ToggleTheme_FromDark_SwitchesToLight()
    {
        // Arrange
        var service = new ThemeService(_mockLogger.Object);
        await service.SetThemeAsync("dark");

        // Act
        await service.ToggleThemeAsync();
        var result = await service.GetCurrentThemeAsync();

        // Assert
        result.Mode.Should().Be("light");
    }

    [Fact]
    public async Task SetCustomColor_UpdatesPrimaryColor()
    {
        // Arrange
        var service = new ThemeService(_mockLogger.Object);
        var customColor = "#FF5733";

        // Act
        await service.SetPrimaryColorAsync(customColor);
        var result = await service.GetCurrentThemeAsync();

        // Assert
        result.PrimaryColor.Should().Be(customColor);
    }

    [Fact]
    public async Task SetCustomColor_WithInvalidHex_ThrowsException()
    {
        // Arrange
        var service = new ThemeService(_mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetPrimaryColorAsync("not-a-color"));
    }

    [Fact]
    public async Task ResetToDefault_RestoresDefaultTheme()
    {
        // Arrange
        var service = new ThemeService(_mockLogger.Object);
        await service.SetThemeAsync("dark");
        await service.SetPrimaryColorAsync("#ABCDEF");

        // Act
        await service.ResetToDefaultAsync();
        var result = await service.GetCurrentThemeAsync();

        // Assert
        result.Mode.Should().Be("light");
        result.PrimaryColor.Should().NotBe("#ABCDEF");
    }

    [Fact]
    public async Task GetThemePreference_ReturnsUserPreferences()
    {
        // Arrange
        var service = new ThemeService(_mockLogger.Object);
        await service.SetThemeAsync("dark");
        await service.SetPrimaryColorAsync("#FF5733");

        // Act
        var prefs = await service.GetThemePreferencesAsync();

        // Assert
        prefs.Mode.Should().Be("dark");
        prefs.PrimaryColor.Should().Be("#FF5733");
        prefs.FontSize.Should().BeGreaterThan(0);
    }
}
