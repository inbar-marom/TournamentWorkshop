namespace TournamentEngine.Console;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TournamentEngine.Console.Configuration;
using TournamentEngine.Console.Services;
using TournamentEngine.Console.Utilities;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Core.BotLoader;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Events;

class Program
{
    static async Task Main(string[] args)
    {
        System.Console.WriteLine("=== Tournament Engine Console ===\n");
        
        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();
        
        // Get logger and configuration accessories
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var tournamentConfig = serviceProvider.GetRequiredService<TournamentConfiguration>();
        var configManager = serviceProvider.GetRequiredService<ConfigurationManager>();
        
        try
        {
            logger.LogInformation("Tournament Engine starting up");
            logger.LogInformation("Environment: {Environment}", System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");
            
            // Log configuration summary
            var configSummary = configManager.GetConfigurationSummary();
            logger.LogInformation("Configuration loaded: {Configs}", string.Join(", ", configSummary.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            
            // Initialize services
            var serviceManager = serviceProvider.GetRequiredService<ServiceManager>();
            var botLoader = serviceProvider.GetRequiredService<IBotLoader>();
            var resultsExporter = serviceProvider.GetRequiredService<ResultsExporter>();
            
            // Create CancellationToken for graceful shutdown
            using var cts = new CancellationTokenSource();
            SetupShutdownHandlers(cts, logger);
            
            // Load bots
            logger.LogInformation("Loading bots from directory");
            var botCount = 0;
            List<BotInfo> bots = new();
            
            if (!string.IsNullOrEmpty(tournamentConfig.TournamentEngine?.BotsDirectory))
            {
                bots = await botLoader.LoadBotsFromDirectoryAsync(tournamentConfig.TournamentEngine.BotsDirectory, cts.Token);
                var validBots = bots.Where(b => b.IsValid).ToList();
                botCount = validBots.Count;
                
                logger.LogInformation("Loaded {TotalBots} bots ({ValidBots} valid)", bots.Count, botCount);
                
                // Log any invalid bots
                var invalidBots = bots.Where(b => !b.IsValid).ToList();
                foreach (var bot in invalidBots)
                {
                    logger.LogWarning("Bot '{TeamName}' failed to load: {Errors}", 
                        bot.TeamName, 
                        string.Join("; ", bot.ValidationErrors));
                }
            }
            else
            {
                logger.LogWarning("No bots directory configured");
            }
            
            // Start dashboard if enabled
            if (tournamentConfig.TournamentEngine?.EnableDashboard ?? false)
            {
                logger.LogInformation("Starting dashboard service");
                if (await serviceManager.StartDashboardAsync())
                {
                    logger.LogInformation("Dashboard started successfully");
                    if (tournamentConfig.TournamentEngine?.DashboardUrl != null)
                    {
                        logger.LogInformation("Dashboard available at: {DashboardUrl}", tournamentConfig.TournamentEngine.DashboardUrl);
                    }
                }
                else
                {
                    logger.LogWarning("Failed to start dashboard - continuing without dashboard");
                }
            }
            
            // Run tournament series
            logger.LogInformation("Starting tournament series execution");
            var seriesManager = serviceProvider.GetRequiredService<TournamentSeriesManager>();
            var seriesResult = await ExecuteTournamentSeriesAsync(
                seriesManager, 
                bots, 
                configManager, 
                logger, 
                cts.Token
            );
            
            if (seriesResult != null)
            {
                logger.LogInformation("Tournament series completed");
                logger.LogInformation("Overall Champion: {Champion}", 
                    seriesResult.Tournaments.LastOrDefault()?.Champion ?? "N/A");
                logger.LogInformation("Total Matches: {TotalMatches}", 
                    seriesResult.Tournaments.Sum(t => t.MatchResults.Count));
                
                // Export results
                logger.LogInformation("Exporting results");
                if (await resultsExporter.ExportSeriesAsync(seriesResult))
                {
                    logger.LogInformation("Results exported successfully");
                }
                else
                {
                    logger.LogWarning("Failed to export results");
                }
            }
            else
            {
                logger.LogWarning("Tournament series execution did not produce results");
            }
            
            logger.LogInformation("Tournament Engine completed successfully");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Tournament Engine was cancelled by user");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tournament Engine encountered an error");
            System.Environment.Exit(1);
        }
        finally
        {
            // Shutdown dashboard if running
            try
            {
                var serviceManager = serviceProvider.GetRequiredService<ServiceManager>();
                var status = serviceManager.GetStatus();
                if (status != ServiceStatus.NotStarted)
                {
                    logger.LogInformation("Shutting down dashboard service");
                    await serviceManager.ShutdownAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during dashboard shutdown");
            }
            
            logger.LogInformation("Tournament Engine shutdown complete");
        }
    }
    
    /// <summary>
    /// Execute a complete tournament series
    /// </summary>
    private static async Task<TournamentSeriesInfo?> ExecuteTournamentSeriesAsync(
        TournamentSeriesManager seriesManager,
        List<BotInfo> bots,
        ConfigurationManager configManager,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        // Filter to valid bots only
        var validBots = bots.Where(b => b.IsValid).ToList();
        
        if (validBots.Count < 2)
        {
            logger.LogError("Cannot run tournament: need at least 2 valid bots, but only {BotCount} available", validBots.Count);
            return null!;
        }
        
        try
        {
            // Create tournament configuration from ConfigurationManager
            var seriesConfig = configManager.CreateSeriesConfig();
            
            // Execute series
            logger.LogInformation("Executing tournament series: {GameCount} game type(s)", seriesConfig.GameTypes.Count);
            
            var seriesResult = await seriesManager.RunSeriesAsync(
                validBots,
                seriesConfig,
                cancellationToken
            );
            
            return seriesResult;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Tournament series execution was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tournament series execution failed");
            throw;
        }
    }
    
    /// <summary>
    /// Configure dependency injection services
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // Add logging first so ConfigurationManager can use it
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        // Add configuration manager
        services.AddSingleton<ConfigurationManager>();
        
        // Get configuration from ConfigurationManager
        var serviceProvider = services.BuildServiceProvider();
        var configManager = serviceProvider.GetRequiredService<ConfigurationManager>();
        var config = configManager.GetConfiguration();
        services.AddSingleton(config);
        
        // Create and register TournamentConfig for Core components
        var tournamentConfig = configManager.CreateTournamentConfig();
        services.AddSingleton(tournamentConfig);
        
        // Re-configure logging with actual log level from config
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            
            var logLevel = config.Logging?.LogLevel?.FirstOrDefault().Value switch
            {
                "Debug" => LogLevel.Debug,
                "Information" => LogLevel.Information,
                "Warning" => LogLevel.Warning,
                "Error" => LogLevel.Error,
                "Critical" => LogLevel.Critical,
                _ => LogLevel.Information
            };
            
            builder.SetMinimumLevel(logLevel);
        });
        
        // Add tournament engine services
        services.AddSingleton<IBotLoader, BotLoader>();
        services.AddSingleton<IGameRunner, GameRunner>();
        services.AddSingleton<IScoringSystem, ScoringSystem>();
        services.AddSingleton<ITournamentEngine, GroupStageTournamentEngine>();
        
        // Add event publisher with dashboard URL from configuration
        services.AddSingleton<ITournamentEventPublisher>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ConsoleEventPublisher>>();
            var dashboardUrl = config.TournamentEngine?.DashboardUrl ?? "http://localhost:5000/tournamentHub";
            return new ConsoleEventPublisher(dashboardUrl, logger);
        });
        
        services.AddSingleton<ITournamentManager, TournamentManager>();
        services.AddSingleton<TournamentSeriesManager>();
        
        // Add application services
        services.AddSingleton<ServiceManager>();
        services.AddSingleton<ResultsExporter>();
    }
    
    /// <summary>
    /// Setup handlers for graceful shutdown on Ctrl+C
    /// </summary>
    private static void SetupShutdownHandlers(CancellationTokenSource cts, ILogger<Program> logger)
    {
        System.Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            logger.LogInformation("Shutdown signal received (Ctrl+C)");
            cts.Cancel();
        };
    }
}
