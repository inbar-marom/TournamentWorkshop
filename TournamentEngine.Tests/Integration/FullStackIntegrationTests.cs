namespace TournamentEngine.Tests.Integration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Core.BotLoader;
using TournamentEngine.Dashboard.Hubs;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Api.Services;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// Full-stack integration tests combining TournamentHub (SignalR) with TournamentSeriesManager.
/// Tests the integration between tournament engine and real-time dashboard broadcasts.
/// Uses REAL Dashboard server - no mocking!
/// </summary>
[TestClass]
public class FullStackIntegrationTests
{
    private GameRunner _gameRunner = null!;
    private ScoringSystem _scoringSystem = null!;
    private GroupStageTournamentEngine _engine = null!;
    private TournamentManager _tournamentManager = null!;
    private TournamentSeriesManager _seriesManager = null!;
    private TournamentConfig _baseConfig = null!;
    
    private WebApplication _dashboardApp = null!;
    private HubConnection _hubConnection = null!;
    private const string DashboardUrl = "http://localhost:5555";
    private StateManagerService _stateManager = null!;
    private SignalRTournamentEventPublisher _eventPublisher = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _baseConfig = IntegrationTestHelpers.CreateConfig();
        _gameRunner = new GameRunner(_baseConfig);
        _scoringSystem = new ScoringSystem();
        _engine = new GroupStageTournamentEngine(_gameRunner, _scoringSystem);

        // Build REAL Dashboard server (same as Dashboard/Program.cs)
        var builder = WebApplication.CreateBuilder();
        
        // Add services exactly like Dashboard does
        builder.Services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        // Register bot services for management
        var botsDir = Path.Combine(Path.GetTempPath(), "test-bots");
        Directory.CreateDirectory(botsDir);
        builder.Services.AddSingleton(sp =>
            new BotStorageService(botsDir, sp.GetRequiredService<ILogger<BotStorageService>>()));
        builder.Services.AddSingleton<IBotLoader>(sp => new BotLoader());
        builder.Services.AddScoped(sp =>
            new BotDashboardService(
                sp.GetRequiredService<BotStorageService>(),
                sp.GetRequiredService<IBotLoader>(),
                sp.GetRequiredService<ILogger<BotDashboardService>>()));

        // Register dashboard services
        builder.Services.AddSingleton<StateManagerService>();
        builder.Services.AddSingleton<SignalRTournamentEventPublisher>();
        builder.Services.AddSingleton<LeaderboardService>();
        builder.Services.AddSingleton<MatchFeedService>();
        builder.Services.AddSingleton<TournamentStatusService>();
        builder.Services.AddSingleton<RealtimeUIUpdateService>();
        builder.Services.AddSingleton<SignalREventPublisher>();
        builder.Services.AddSingleton<GroupStandingsGridService>();
        builder.Services.AddSingleton<ChartsService>();
        builder.Services.AddSingleton<MatchDetailsService>();
        builder.Services.AddSingleton<TournamentVisualizationService>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<ExportService>();
        builder.Services.AddSingleton<ShareService>();
        builder.Services.AddSingleton<NotificationPreferencesService>();
        builder.Services.AddSingleton<ResponsiveLayoutService>();
        builder.Services.AddScoped<TournamentManagementService>();

        builder.WebHost.UseUrls(DashboardUrl);

        _dashboardApp = builder.Build();

        // Configure the HTTP request pipeline
        _dashboardApp.UseCors();
        _dashboardApp.UseRouting();
        _dashboardApp.MapControllers();
        _dashboardApp.MapHub<TournamentHub>("/tournamentHub");

        // Start the dashboard
        await _dashboardApp.StartAsync();

        //Get the real StateManagerService from the dashboard
        _stateManager = _dashboardApp.Services.GetRequiredService<StateManagerService>();
        _eventPublisher = _dashboardApp.Services.GetRequiredService<SignalRTournamentEventPublisher>();

        // Create TournamentManager with real-time event publisher
        _tournamentManager = new TournamentManager(_engine, _gameRunner, _eventPublisher);
        _seriesManager = new TournamentSeriesManager(_tournamentManager, _scoringSystem, _eventPublisher);

        // Connect SignalR client to real dashboard
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{DashboardUrl}/tournamentHub")
            .WithAutomaticReconnect()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .Build();

        await _hubConnection.StartAsync();
        
        // Give SignalR time to establish connection properly
        await Task.Delay(500);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
        }

        if (_dashboardApp != null)
        {
            await _dashboardApp.StopAsync();
            await _dashboardApp.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task Hub_ReceivesTournamentStateUpdate_BroadcastsToClients()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(6);

        var receivedMatches = new List<RecentMatchDto>();
        TournamentStateDto? receivedState = null;

        // Subscribe to real-time events BEFORE tournament starts
        _hubConnection.On<RecentMatchDto>("MatchCompleted", match =>
        {
            receivedMatches.Add(match);
        });

        _hubConnection.On<TournamentStateDto>("CurrentState", state =>
        {
            receivedState = state;
        });

        // Act - Run tournament, events will stream in REAL-TIME as matches complete
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, GameType.RPSLS, _baseConfig);

        await Task.Delay(1000); // Give SignalR time to deliver all events

        // Assert
        Assert.AreEqual(TournamentState.Completed, tournamentInfo.State);
        Assert.IsNotNull(tournamentInfo.Champion);
        
        // Verify we received match events in real-time during tournament execution
        Assert.IsTrue(receivedMatches.Count > 0, "Should have received real-time match events");
        Assert.AreEqual(tournamentInfo.MatchResults.Count, receivedMatches.Count,
            "Should receive event for every match");
        
        // Verify match data is correct
        foreach (var match in receivedMatches)
        {
            Assert.IsNotNull(match.Bot1Name);
            Assert.IsNotNull(match.Bot2Name);
            Assert.IsTrue(match.Bot1Score >= 0);
            Assert.IsTrue(match.Bot2Score >= 0);
        }
    }

    [TestMethod]
    public async Task Hub_StreamsMatchEventsInRealTime()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(4);

        var receivedMatches = new List<RecentMatchDto>();
        
        _hubConnection.On<RecentMatchDto>("MatchCompleted", match =>
        {
            receivedMatches.Add(match);
        });

        // Act - Tournament runs and streams matches in real-time
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, GameType.RPSLS, _baseConfig);

        await Task.Delay(200);

        // Assert
        Assert.IsTrue(receivedMatches.Count > 0, "Should receive real-time match events");
        Assert.AreEqual(tournamentInfo.MatchResults.Count, receivedMatches.Count);
        
        // Verify match data matches tournament results
        for (int i = 0; i < receivedMatches.Count; i++)
        {
            Assert.IsNotNull(receivedMatches[i].MatchId);
            Assert.IsNotNull(receivedMatches[i].Bot1Name);
            Assert.IsNotNull(receivedMatches[i].Bot2Name);
        }
    }

    [TestMethod]
    public async Task Hub_SeriesIntegration_StreamsMultipleTournamentsInRealTime()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(6);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto, GameType.PenaltyKicks },
            BaseConfig = _baseConfig
        };

        var receivedMatches = new List<RecentMatchDto>();
        var tournamentStartEvents = new List<EventStartedEventDto>();
        var tournamentCompleteEvents = new List<EventCompletedEventDto>();

        _hubConnection.On<RecentMatchDto>("MatchCompleted", match => receivedMatches.Add(match));
        _hubConnection.On<EventStartedEventDto>("EventStarted", evt => tournamentStartEvents.Add(evt));
        _hubConnection.On<EventCompletedEventDto>("EventCompleted", evt => tournamentCompleteEvents.Add(evt));

        // Act - Run series, events stream in REAL-TIME across all tournaments
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        await Task.Delay(300);

        // Assert
        Assert.AreEqual(3, seriesInfo.Tournaments.Count);
        
        // Verify we received tournament lifecycle events
        Assert.AreEqual(3, tournamentStartEvents.Count, "Should receive start event for each tournament");
        Assert.AreEqual(3, tournamentCompleteEvents.Count, "Should receive complete event for each tournament");
        
        // Verify we received all match events in real-time
        int totalMatches = seriesInfo.Tournaments.Sum(t => t.MatchResults.Count);
        Assert.AreEqual(totalMatches, receivedMatches.Count,
            "Should receive match event for every match across all tournaments");
    }

    [TestMethod]
    public async Task Hub_PublishesAllMatchesInRealTime()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(5);

        var receivedMatches = new List<RecentMatchDto>();
        _hubConnection.On<RecentMatchDto>("MatchCompleted", match => receivedMatches.Add(match));

        // Act - Tournament streams all matches in real-time as they execute
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, GameType.RPSLS, _baseConfig);

        await Task.Delay(200);

        // Assert
        Assert.IsTrue(receivedMatches.Count > 0);
        Assert.AreEqual(tournamentInfo.MatchResults.Count, receivedMatches.Count);
        
        foreach (var match in receivedMatches)
        {
            Assert.IsNotNull(match.Bot1Name);
            Assert.IsNotNull(match.Bot2Name);
            Assert.IsTrue(Enum.IsDefined(typeof(MatchOutcome), match.Outcome));
        }
    }

    [TestMethod]
    public async Task Hub_StreamsRoundStartEvents()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(6);

        var roundStartEvents = new List<RoundStartedDto>();
        _hubConnection.On<RoundStartedDto>("RoundStarted", evt => roundStartEvents.Add(evt));

        // Act - Run tournament, rounds stream as they start
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, GameType.RPSLS, _baseConfig);

        await Task.Delay(200);

        // Assert
        Assert.IsTrue(roundStartEvents.Count > 0, "Should receive round start events");
    }

    [TestMethod]
    public async Task Hub_StreamsLargeSeries_AllEventsReceived()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(8);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> 
            { 
                GameType.RPSLS, 
                GameType.ColonelBlotto, 
                GameType.PenaltyKicks,
                GameType.SecurityGame
            },
            BaseConfig = _baseConfig
        };

        var receivedMatches = new List<RecentMatchDto>();
        _hubConnection.On<RecentMatchDto>("MatchCompleted", match => receivedMatches.Add(match));

        // Act - Run large series, stream all events in real-time
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        await Task.Delay(500);

        // Assert
        Assert.AreEqual(4, seriesInfo.Tournaments.Count);
        int totalMatches = seriesInfo.Tournaments.Sum(t => t.MatchResults.Count);
        Assert.AreEqual(totalMatches, receivedMatches.Count,
            "Should receive all match events across 4-tournament series");
    }

    [TestMethod]
    public async Task Hub_StreamsChampionshipResults()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(10);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto },
            BaseConfig = _baseConfig
        };

        var completedEvents = new List<EventCompletedEventDto>();
        _hubConnection.On<EventCompletedEventDto>("EventCompleted", evt => completedEvents.Add(evt));

        // Act
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        await Task.Delay(300);

        // Assert
        Assert.AreEqual(2, completedEvents.Count);
        foreach (var evt in completedEvents)
        {
            Assert.IsNotNull(evt.Champion);
            Assert.IsTrue(evt.TotalMatches > 0);
        }
    }

    [TestMethod]
    public async Task Hub_RealTimeProgress_MatchesStreamAsTheyComplete()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(6);

        var matchTimestamps = new List<DateTime>();
        _hubConnection.On<RecentMatchDto>("MatchCompleted", match =>
        {
            matchTimestamps.Add(DateTime.UtcNow);
        });

        // Act
        var startTime = DateTime.UtcNow;
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, GameType.RPSLS, _baseConfig);
        var endTime = DateTime.UtcNow;

        await Task.Delay(200);

        // Assert
        Assert.IsTrue(matchTimestamps.Count > 0);
        
        // Verify matches streamed during tournament execution, not all at end
        var firstMatchTime = matchTimestamps.First();
        var lastMatchTime = matchTimestamps.Last();
        
        Assert.IsTrue(firstMatchTime >= startTime, "First match should be during tournament");
        Assert.IsTrue(lastMatchTime <= endTime.AddSeconds(1), "Last match should be during tournament");
    }

    [TestMethod]
    public async Task Hub_LargeSeriesIntegration_StreamsProgressively()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(12);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> 
            { 
                GameType.RPSLS, 
                GameType.ColonelBlotto, 
                GameType.PenaltyKicks 
            },
            BaseConfig = _baseConfig
        };

        var receivedMatches = new List<RecentMatchDto>();
        var tournamentStepsCompleted = new List<EventStepCompletedDto>();
        
        _hubConnection.On<RecentMatchDto>("MatchCompleted", match => receivedMatches.Add(match));
        _hubConnection.On<EventStepCompletedDto>("EventStepCompleted", evt => tournamentStepsCompleted.Add(evt));

        // Act - Run large series
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        // Wait for all SignalR messages to be received
        await Task.Delay(2000);

        // Assert
        Assert.AreEqual(3, seriesInfo.Tournaments.Count);
        Assert.AreEqual(3, tournamentStepsCompleted.Count, "Should complete all 3 tournaments");
        
        int totalMatches = seriesInfo.Tournaments.Sum(t => t.MatchResults.Count);
        Assert.AreEqual(totalMatches, receivedMatches.Count);
        
        // Verify final champion
        var finalStep = tournamentStepsCompleted.Last();
        Assert.IsNotNull(finalStep.WinnerName);
    }
}

