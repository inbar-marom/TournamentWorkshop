using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for managing theme preferences (dark mode, colors, etc.)
/// Phase 6: Polish & Features - Theme Management
/// </summary>
public class ThemeService
{
    private readonly ILogger<ThemeService> _logger;
    private ThemeDto _currentTheme;
    private readonly Dictionary<string, ThemeDto> _availableThemes;

    public ThemeService(ILogger<ThemeService> logger)
    {
        _logger = logger;
        
        // Initialize available themes
        _availableThemes = new Dictionary<string, ThemeDto>
        {
            ["light"] = new ThemeDto
            {
                Name = "light",
                Mode = "light",
                PrimaryColor = "#3b82f6",
                BackgroundColor = "#ffffff",
                TextColor = "#1f2937"
            },
            ["dark"] = new ThemeDto
            {
                Name = "dark",
                Mode = "dark",
                PrimaryColor = "#60a5fa",
                BackgroundColor = "#1f2937",
                TextColor = "#f9fafb"
            }
        };

        // Default to light theme
        _currentTheme = _availableThemes["light"];
    }

    public Task<ThemeDto> GetCurrentThemeAsync()
    {
        return Task.FromResult(_currentTheme);
    }

    public Task SetThemeAsync(string mode)
    {
        if (!_availableThemes.ContainsKey(mode))
        {
            throw new ArgumentException($"Invalid theme mode: {mode}. Valid options are: {string.Join(", ", _availableThemes.Keys)}");
        }

        _currentTheme = new ThemeDto
        {
            Name = mode,
            Mode = mode,
            PrimaryColor = _availableThemes[mode].PrimaryColor,
            BackgroundColor = _availableThemes[mode].BackgroundColor,
            TextColor = _availableThemes[mode].TextColor
        };

        _logger.LogInformation("Theme changed to {Mode}", mode);
        return Task.CompletedTask;
    }

    public Task<List<ThemeDto>> GetAvailableThemesAsync()
    {
        return Task.FromResult(_availableThemes.Values.ToList());
    }

    public async Task ToggleThemeAsync()
    {
        var newMode = _currentTheme.Mode == "light" ? "dark" : "light";
        await SetThemeAsync(newMode);
    }

    public Task SetPrimaryColorAsync(string hexColor)
    {
        if (!IsValidHexColor(hexColor))
        {
            throw new ArgumentException($"Invalid hex color: {hexColor}");
        }

        _currentTheme.PrimaryColor = hexColor;
        return Task.CompletedTask;
    }

    public async Task ResetToDefaultAsync()
    {
        await SetThemeAsync("light");
    }

    public Task<ThemePreferencesDto> GetThemePreferencesAsync()
    {
        return Task.FromResult(new ThemePreferencesDto
        {
            Mode = _currentTheme.Mode,
            PrimaryColor = _currentTheme.PrimaryColor,
            FontSize = 14
        });
    }

    private bool IsValidHexColor(string hexColor)
    {
        if (string.IsNullOrWhiteSpace(hexColor))
            return false;

        if (!hexColor.StartsWith("#"))
            return false;

        if (hexColor.Length != 7 && hexColor.Length != 4)
            return false;

        return hexColor.Skip(1).All(c => char.IsDigit(c) || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'));
    }
}

/// <summary>
/// Theme configuration DTO
/// </summary>
public class ThemeDto
{
    public string Name { get; set; } = "";
    public string Mode { get; set; } = "";
    public string PrimaryColor { get; set; } = "";
    public string BackgroundColor { get; set; } = "";
    public string TextColor { get; set; } = "";
}

/// <summary>
/// Theme preferences DTO
/// </summary>
public class ThemePreferencesDto
{
    public string Mode { get; set; } = "";
    public string PrimaryColor { get; set; } = "";
    public int FontSize { get; set; }
}
