using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using TournamentEngine.Dashboard.Services;

namespace TournamentEngine.Dashboard.Tests.UI;

public class ResponsiveLayoutServiceTests
{
    private Mock<ILogger<ResponsiveLayoutService>> _mockLogger;

    public ResponsiveLayoutServiceTests()
    {
        _mockLogger = new Mock<ILogger<ResponsiveLayoutService>>();
    }

    [Fact]
    public async Task DetectDevice_IdentifiesMobile()
    {
        // Arrange
        var service = new ResponsiveLayoutService(_mockLogger.Object);
        var userAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 14_0 like Mac OS X)";

        // Act
        var result = await service.DetectDeviceTypeAsync(userAgent);

        // Assert
        result.Should().Be("mobile");
    }

    [Fact]
    public async Task DetectDevice_IdentifiesTablet()
    {
        // Arrange
        var service = new ResponsiveLayoutService(_mockLogger.Object);
        var userAgent = "Mozilla/5.0 (iPad; CPU OS 14_0 like Mac OS X)";

        // Act
        var result = await service.DetectDeviceTypeAsync(userAgent);

        // Assert
        result.Should().Be("tablet");
    }

    [Fact]
    public async Task DetectDevice_IdentifiesDesktop()
    {
        // Arrange
        var service = new ResponsiveLayoutService(_mockLogger.Object);
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

        // Act
        var result = await service.DetectDeviceTypeAsync(userAgent);

        // Assert
        result.Should().Be("desktop");
    }

    [Fact]
    public async Task GetLayoutConfig_ReturnsMobileLayout()
    {
        // Arrange
        var service = new ResponsiveLayoutService(_mockLogger.Object);

        // Act
        var config = await service.GetLayoutConfigAsync("mobile");

        // Assert
        config.Should().NotBeNull();
        config.DeviceType.Should().Be("mobile");
        config.ShowSidebar.Should().BeFalse();
        config.CompactMode.Should().BeTrue();
    }

    [Fact]
    public async Task GetLayoutConfig_ReturnsDesktopLayout()
    {
        // Arrange
        var service = new ResponsiveLayoutService(_mockLogger.Object);

        // Act
        var config = await service.GetLayoutConfigAsync("desktop");

        // Assert
        config.Should().NotBeNull();
        config.DeviceType.Should().Be("desktop");
        config.ShowSidebar.Should().BeTrue();
        config.CompactMode.Should().BeFalse();
    }

    [Fact]
    public async Task GetBreakpoints_ReturnsStandardBreakpoints()
    {
        // Arrange
        var service = new ResponsiveLayoutService(_mockLogger.Object);

        // Act
        var breakpoints = await service.GetBreakpointsAsync();

        // Assert
        breakpoints.Should().ContainKey("mobile");
        breakpoints.Should().ContainKey("tablet");
        breakpoints.Should().ContainKey("desktop");
        breakpoints["mobile"].Should().BeLessThan(breakpoints["tablet"]);
        breakpoints["tablet"].Should().BeLessThan(breakpoints["desktop"]);
    }

    [Fact]
    public async Task SetPreferredLayout_UpdatesUserPreference()
    {
        // Arrange
        var service = new ResponsiveLayoutService(_mockLogger.Object);

        // Act
        await service.SetPreferredLayoutAsync("compact");
        var prefs = await service.GetUserLayoutPreferencesAsync();

        // Assert
        prefs.PreferredLayout.Should().Be("compact");
    }

    [Fact]
    public async Task SetPreferredLayout_WithInvalidLayout_ThrowsException()
    {
        // Arrange
        var service = new ResponsiveLayoutService(_mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetPreferredLayoutAsync("invalid"));
    }

    [Fact]
    public async Task GetAvailableLayouts_ReturnsLayoutOptions()
    {
        // Arrange
        var service = new ResponsiveLayoutService(_mockLogger.Object);

        // Act
        var layouts = await service.GetAvailableLayoutsAsync();

        // Assert
        layouts.Should().Contain(new[] { "default", "compact", "wide" });
        layouts.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task SetColumnCount_UpdatesGridLayout()
    {
        // Arrange
        var service = new ResponsiveLayoutService(_mockLogger.Object);

        // Act
        await service.SetColumnCountAsync(2);
        var config = await service.GetUserLayoutPreferencesAsync();

        // Assert
        config.ColumnCount.Should().Be(2);
    }

    [Fact]
    public async Task SetColumnCount_WithInvalidCount_ThrowsException()
    {
        // Arrange
        var service = new ResponsiveLayoutService(_mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetColumnCountAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetColumnCountAsync(5));
    }

    [Fact]
    public async Task GetOptimalLayout_ForScreenSize_ReturnsBestFit()
    {
        // Arrange
        var service = new ResponsiveLayoutService(_mockLogger.Object);

        // Act
        var layoutSmall = await service.GetOptimalLayoutAsync(width: 375, height: 667);  // iPhone
        var layoutLarge = await service.GetOptimalLayoutAsync(width: 1920, height: 1080); // Desktop

        // Assert
        layoutSmall.CompactMode.Should().BeTrue();
        layoutSmall.ColumnCount.Should().Be(1);
        layoutLarge.CompactMode.Should().BeFalse();
        layoutLarge.ColumnCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task SetSidebarCollapsed_UpdatesState()
    {
        // Arrange
        var service = new ResponsiveLayoutService(_mockLogger.Object);

        // Act
        await service.SetSidebarCollapsedAsync(true);
        var prefs = await service.GetUserLayoutPreferencesAsync();

        // Assert
        prefs.SidebarCollapsed.Should().BeTrue();
    }

    [Fact]
    public async Task ResetToDefaults_RestoresDefaultLayout()
    {
        // Arrange
        var service = new ResponsiveLayoutService(_mockLogger.Object);
        await service.SetPreferredLayoutAsync("compact");
        await service.SetColumnCountAsync(1);
        await service.SetSidebarCollapsedAsync(true);

        // Act
        await service.ResetToDefaultsAsync();
        var prefs = await service.GetUserLayoutPreferencesAsync();

        // Assert
        prefs.PreferredLayout.Should().Be("default");
        prefs.ColumnCount.Should().BeGreaterThan(1);
        prefs.SidebarCollapsed.Should().BeFalse();
    }
}
