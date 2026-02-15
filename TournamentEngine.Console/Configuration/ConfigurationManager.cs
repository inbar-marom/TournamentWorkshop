namespace TournamentEngine.Console.Configuration;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TournamentEngine.Core.Common;

/// <summary>
/// Manages tournament configuration from appsettings.json, environment variables, and defaults
/// </summary>
public class ConfigurationManager
{
    private readonly ILogger<ConfigurationManager> _logger;
    private readonly IConfiguration _configuration;
    private TournamentConfiguration? _config;

    public ConfigurationManager(ILogger<ConfigurationManager> logger)
    {
        _logger = logger;
        _configuration = BuildConfiguration();
    }

    /// <summary>
    /// Load and return the complete tournament configuration
    /// </summary>
    public TournamentConfiguration GetConfiguration()
    {
        if (_config == null)
        {
            _config = LoadAndValidateConfiguration();
        }
        return _config ?? throw new InvalidOperationException("Configuration failed to load");
    }

    /// <summary>
    /// Create TournamentConfig for tournament engine from current settings
    /// </summary>
    public TournamentConfig CreateTournamentConfig()
    {
        var config = GetConfiguration();
        return new TournamentConfig
        {
            Games = config.TournamentEngine?.DefaultGameTypes ?? new List<GameType>(),
            ImportTimeout = TimeSpan.FromSeconds(config.TournamentEngine?.BotLoadingTimeout ?? 30),
            MoveTimeout = TimeSpan.FromSeconds(config.TournamentEngine?.MoveTimeout ?? 5),
            MemoryLimitMB = config.TournamentEngine?.MemoryLimitMB ?? 512,
            MaxRoundsRPSLS = config.TournamentEngine?.MaxRoundsRPSLS ?? 50,
            LogLevel = config.TournamentEngine?.LogLevel ?? "Information",
            BotsDirectory = config.TournamentEngine?.BotsDirectory ?? "./bots",
            ResultsFilePath = Path.Combine(config.TournamentEngine?.ResultsDirectory ?? "./results", "results.json")
        };
    }

    /// <summary>
    /// Create TournamentSeriesConfig for series manager from current settings
    /// </summary>
    public TournamentSeriesConfig CreateSeriesConfig()
    {
        var config = GetConfiguration();
        return new TournamentSeriesConfig
        {
            GameTypes = config.TournamentEngine?.DefaultGameTypes ?? new List<GameType>(),
            BaseConfig = CreateTournamentConfig(),
            SeriesName = config.TournamentSeries?.Name
        };
    }

    /// <summary>
    /// Get dictionary of all configuration values for logging/display
    /// </summary>
    public Dictionary<string, object> GetConfigurationSummary()
    {
        var config = GetConfiguration();
        return new Dictionary<string, object>
        {
            { "Tournament Series Name", config.TournamentSeries?.Name ?? "Unknown" },
            { "Bot Count", config.TournamentSeries?.BotCount ?? 0 },
            { "Games", string.Join(", ", config.TournamentEngine?.DefaultGameTypes ?? new List<GameType>()) },
            { "Bots Directory", Path.GetFullPath(config.TournamentEngine?.BotsDirectory ?? "./bots") },
            { "Results Directory", Path.GetFullPath(config.TournamentEngine?.ResultsDirectory ?? "./results") },
            { "Max Parallel Matches", config.TournamentEngine?.MaxParallelMatches ?? 5 },
            { "Move Timeout (sec)", config.TournamentEngine?.MoveTimeout ?? 5 },
            { "Dashboard Enabled", config.TournamentEngine?.EnableDashboard ?? false },
            { "Dashboard URL", config.TournamentEngine?.DashboardUrl ?? "http://localhost:5000" },
            { "Export Results", config.TournamentSeries?.ExportResults ?? true },
            { "Seed Randomly", config.TournamentSeries?.SeedRandomly ?? true }
        };
    }

    /// <summary>
    /// Build IConfiguration from appsettings files and environment variables
    /// </summary>
    private IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";

        _logger?.LogInformation("Building configuration for environment: {Environment}", environment);

        var basePath = ResolveConfigBasePath();
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("TOURNAMENT_");

        return builder.Build();
    }

    private static string ResolveConfigBasePath()
    {
        var currentDir = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(currentDir, "appsettings.json")))
        {
            return currentDir;
        }

        var baseDir = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(baseDir, "appsettings.json")))
        {
            return baseDir;
        }

        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            var consoleConfig = Path.Combine(dir.FullName, "TournamentEngine.Console", "appsettings.json");
            if (File.Exists(consoleConfig))
            {
                return Path.GetDirectoryName(consoleConfig) ?? dir.FullName;
            }
            dir = dir.Parent;
        }

        return currentDir;
    }

    /// <summary>
    /// Load configuration and validate all required settings
    /// </summary>
    private TournamentConfiguration LoadAndValidateConfiguration()
    {
        var config = new TournamentConfiguration();
        _configuration.Bind("TournamentEngine", config.TournamentEngine = new TournamentEngineSettings { });
        _configuration.Bind("TournamentSeries", config.TournamentSeries = new TournamentSeriesSettings { });
        _configuration.Bind("Logging", config.Logging = new LoggingSettings { });

        _logger.LogInformation("Configuration loaded successfully");
        ValidateConfiguration(config);
        EnsureDirectoriesExist(config);

        return config;
    }

    /// <summary>
    /// Validate all required configuration values
    /// </summary>
    private void ValidateConfiguration(TournamentConfiguration? config)
    {
        if (config == null)
            throw new InvalidOperationException("Configuration object is null");

        var errors = new List<string>();

        // Required string fields
        if (string.IsNullOrWhiteSpace(config.TournamentEngine?.BotsDirectory))
            errors.Add("TournamentEngine:BotsDirectory is required");
        if (string.IsNullOrWhiteSpace(config.TournamentEngine?.ResultsDirectory))
            errors.Add("TournamentEngine:ResultsDirectory is required");
        if (string.IsNullOrWhiteSpace(config.TournamentSeries?.Name))
            errors.Add("TournamentSeries:Name is required");

        // Numeric ranges
        if ((config.TournamentEngine?.BotLoadingTimeout ?? 0) <= 0)
            errors.Add("TournamentEngine:BotLoadingTimeout must be > 0");
        if ((config.TournamentEngine?.MoveTimeout ?? 0) <= 0)
            errors.Add("TournamentEngine:MoveTimeout must be > 0");
        if ((config.TournamentEngine?.MemoryLimitMB ?? 0) <= 0)
            errors.Add("TournamentEngine:MemoryLimitMB must be > 0");
        if ((config.TournamentEngine?.MaxParallelMatches ?? 0) <= 0)
            errors.Add("TournamentEngine:MaxParallelMatches must be > 0");

        // Game types
        if (config.TournamentEngine?.DefaultGameTypes == null || config.TournamentEngine.DefaultGameTypes.Count == 0)
            errors.Add("TournamentEngine:DefaultGameTypes must contain at least one game");

        // Series configuration
        if ((config.TournamentSeries?.BotCount ?? 0) < 2)
            errors.Add("TournamentSeries:BotCount must be >= 2");

        if (errors.Count > 0)
        {
            var errorMessage = string.Join(Environment.NewLine, errors);
            _logger.LogError("Configuration validation failed:{NewLine}{Errors}", Environment.NewLine, errorMessage);
            throw new InvalidOperationException($"Configuration validation failed:\n{errorMessage}");
        }

        _logger.LogInformation("Configuration validated successfully");
    }

    /// <summary>
    /// Ensure all required directories exist, creating them if necessary
    /// </summary>
    private void EnsureDirectoriesExist(TournamentConfiguration? config)
    {
        if (config?.TournamentEngine == null)
            return;

        var directories = new[] 
        { 
            config.TournamentEngine.BotsDirectory ?? "./bots",
            config.TournamentEngine.ResultsDirectory ?? "./results"
        };

        foreach (var dir in directories)
        {
            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    _logger.LogInformation("Created directory: {Directory}", dir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create directory {Directory}: {Message}", dir, ex.Message);
            }
        }
    }
}
