namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for managing responsive layout and mobile design
/// Phase 6: Polish & Features - Responsive Design
/// </summary>
public class ResponsiveLayoutService
{
    private readonly ILogger<ResponsiveLayoutService> _logger;
    private UserLayoutPreferencesDto _userPreferences;
    private readonly Dictionary<string, int> _breakpoints;
    private readonly string[] _validLayouts = new[] { "default", "compact", "wide" };

    public ResponsiveLayoutService(ILogger<ResponsiveLayoutService> logger)
    {
        _logger = logger;

        _breakpoints = new Dictionary<string, int>
        {
            ["mobile"] = 768,
            ["tablet"] = 1024,
            ["desktop"] = 1920
        };

        _userPreferences = new UserLayoutPreferencesDto
        {
            PreferredLayout = "default",
            ColumnCount = 3,
            SidebarCollapsed = false
        };
    }

    public Task<string> DetectDeviceTypeAsync(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return Task.FromResult("desktop");

        userAgent = userAgent.ToLower();

        if (userAgent.Contains("iphone") || userAgent.Contains("android") && !userAgent.Contains("tablet"))
            return Task.FromResult("mobile");

        if (userAgent.Contains("ipad") || userAgent.Contains("tablet"))
            return Task.FromResult("tablet");

        return Task.FromResult("desktop");
    }

    public Task<LayoutConfigDto> GetLayoutConfigAsync(string deviceType)
    {
        var config = new LayoutConfigDto
        {
            DeviceType = deviceType
        };

        switch (deviceType)
        {
            case "mobile":
                config.ShowSidebar = false;
                config.CompactMode = true;
                config.ColumnCount = 1;
                break;
            case "tablet":
                config.ShowSidebar = false;
                config.CompactMode = true;
                config.ColumnCount = 2;
                break;
            case "desktop":
            default:
                config.ShowSidebar = true;
                config.CompactMode = false;
                config.ColumnCount = 3;
                break;
        }

        return Task.FromResult(config);
    }

    public Task<Dictionary<string, int>> GetBreakpointsAsync()
    {
        return Task.FromResult(_breakpoints);
    }

    public Task SetPreferredLayoutAsync(string layout)
    {
        if (!_validLayouts.Contains(layout))
        {
            throw new ArgumentException($"Invalid layout: {layout}. Valid options are: {string.Join(", ", _validLayouts)}");
        }

        _userPreferences.PreferredLayout = layout;
        return Task.CompletedTask;
    }

    public Task<UserLayoutPreferencesDto> GetUserLayoutPreferencesAsync()
    {
        return Task.FromResult(_userPreferences);
    }

    public Task<string[]> GetAvailableLayoutsAsync()
    {
        return Task.FromResult(_validLayouts);
    }

    public Task SetColumnCountAsync(int count)
    {
        if (count < 1 || count > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Column count must be between 1 and 4");
        }

        _userPreferences.ColumnCount = count;
        return Task.CompletedTask;
    }

    public Task<LayoutConfigDto> GetOptimalLayoutAsync(int width, int height)
    {
        var config = new LayoutConfigDto();

        if (width < _breakpoints["mobile"])
        {
            config.DeviceType = "mobile";
            config.CompactMode = true;
            config.ColumnCount = 1;
            config.ShowSidebar = false;
        }
        else if (width < _breakpoints["tablet"])
        {
            config.DeviceType = "tablet";
            config.CompactMode = true;
            config.ColumnCount = 2;
            config.ShowSidebar = false;
        }
        else
        {
            config.DeviceType = "desktop";
            config.CompactMode = false;
            config.ColumnCount = width >= _breakpoints["desktop"] ? 4 : 3;
            config.ShowSidebar = true;
        }

        return Task.FromResult(config);
    }

    public Task SetSidebarCollapsedAsync(bool collapsed)
    {
        _userPreferences.SidebarCollapsed = collapsed;
        return Task.CompletedTask;
    }

    public Task ResetToDefaultsAsync()
    {
        _userPreferences = new UserLayoutPreferencesDto
        {
            PreferredLayout = "default",
            ColumnCount = 3,
            SidebarCollapsed = false
        };
        return Task.CompletedTask;
    }
}

/// <summary>
/// Layout configuration DTO
/// </summary>
public class LayoutConfigDto
{
    public string DeviceType { get; set; } = "";
    public bool ShowSidebar { get; set; }
    public bool CompactMode { get; set; }
    public int ColumnCount { get; set; }
}

/// <summary>
/// User layout preferences DTO
/// </summary>
public class UserLayoutPreferencesDto
{
    public string PreferredLayout { get; set; } = "default";
    public int ColumnCount { get; set; }
    public bool SidebarCollapsed { get; set; }
}
