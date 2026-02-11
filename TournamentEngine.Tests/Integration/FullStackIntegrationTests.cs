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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Dashboard.Hubs;
using TournamentEngine.Dashboard.Services;
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

    [TestInitialize]
    public async Task Setup()
    {
        _baseConfig = IntegrationTestHelpers.CreateConfig();
        _gameRunner = new GameRunner(_baseConfig);
        _scoringSystem = new ScoringSystem();
        _engine = new GroupStageTournamentEngine(_gameRunner, _scoringSystem);
        _tournamentManager = new TournamentManager(_engine, _gameRunner);
        _seriesManager = new TournamentSeriesManager(_tournamentManager, _scoringSystem);

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

        // Register dashboard services
        builder.Services.AddSingleton<StateManagerService>();
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

        builder.WebHost.UseUrls(DashboardUrl);

        _dashboardApp = builder.Build();

        // Configure the HTTP request pipeline
        _dashboardApp.UseCors();
        _dashboardApp.UseRouting();
        _dashboardApp.MapControllers();
        _dashboardApp.MapHub<TournamentHub>("/tournamentHub");

        // Start the dashboard
        await _dashboardApp.StartAsync();

        // Get the real StateManagerService from the dashboard
        _stateManager = _dashboardApp.Services.GetRequiredService<StateManagerService>();

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
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, GameType.RPSLS, _baseConfig);

        var stateDto = ConvertToTournamentStateDto(tournamentInfo);

        TournamentStateDto? receivedState = null;
        _hubConnection.On<TournamentStateDto>("CurrentState", state => receivedState = state);

        // Act - Call REAL Hub method via SignalR
        await _hubConnection.InvokeAsync("PublishStateUpdate", stateDto);
        await Task.Delay(100); // Give time for SignalR to broadcast

        // Assert
        Assert.AreEqual(TournamentStatus.Completed, stateDto.Status);
        Assert.IsNotNull(stateDto.CurrentTournament);
        Assert.IsTrue(stateDto.OverallLeaderboard.Count > 0);
        
        var currentState = await _stateManager.GetCurrentStateAsync();
        Assert.IsNotNull(currentState);
        Assert.AreEqual(TournamentStatus.Completed, currentState.Status);
        
        Assert.IsNotNull(receivedState, "Should have received state broadcast");
        Assert.AreEqual(TournamentStatus.Completed, receivedState.Status);
    }

    [TestMethod]
    public async Task Hub_ReceivesMatchCompletedEvent_BroadcastsToClients()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(4);
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, GameType.RPSLS, _baseConfig);

        var firstMatch = tournamentInfo.MatchResults.First();
        var matchDto = ConvertToRecentMatchDto(firstMatch);

        RecentMatchDto? receivedMatch = null;
        _hubConnection.On<RecentMatchDto>("MatchCompleted", match => receivedMatch = match);

        // Act - Call REAL Hub method via SignalR
        await _hubConnection.InvokeAsync("PublishMatchCompleted", matchDto);
        await Task.Delay(100);

        // Assert
        Assert.IsNotNull(matchDto.MatchId);
        Assert.IsNotNull(matchDto.Bot1Name);
        Assert.IsNotNull(matchDto.Bot2Name);
        Assert.IsTrue(matchDto.Bot1Score >= 0);
        Assert.IsTrue(matchDto.Bot2Score >= 0);
        
        Assert.IsNotNull(receivedMatch, "Should have received match broadcast");
        Assert.AreEqual(matchDto.Bot1Name, receivedMatch.Bot1Name);
        Assert.AreEqual(matchDto.Bot2Name, receivedMatch.Bot2Name);
    }

    [TestMethod]
    public async Task Hub_SeriesIntegration_PublishesMultipleTournamentUpdates()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(6);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto, GameType.PenaltyKicks },
            BaseConfig = _baseConfig
        };

        // Act - Run series and publish updates for each tournament via REAL Hub
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        int updateCount = 0;
        foreach (var tournament in seriesInfo.Tournaments)
        {
            var stateDto = ConvertToTournamentStateDto(tournament, seriesInfo, updateCount);
            await _hubConnection.InvokeAsync("PublishStateUpdate", stateDto);
            updateCount++;
        }
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(3, updateCount);
        
        var finalState = await _stateManager.GetCurrentStateAsync();
        Assert.IsNotNull(finalState);
        Assert.AreEqual(TournamentStatus.Completed, finalState.Status);
    }

    [TestMethod]
    public async Task Hub_PublishesAllMatchesFromTournament()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(5);
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, GameType.RPSLS, _baseConfig);

        // Act - Publish all match completed events via REAL Hub
        List<RecentMatchDto> publishedMatches = new();
        foreach (var match in tournamentInfo.MatchResults)
        {
            var matchDto = ConvertToRecentMatchDto(match);
            publishedMatches.Add(matchDto);
            await _hubConnection.InvokeAsync("PublishMatchCompleted", matchDto);
        }
        await Task.Delay(100);

        // Assert
        Assert.IsTrue(publishedMatches.Count > 0);
        Assert.AreEqual(tournamentInfo.MatchResults.Count, publishedMatches.Count);
        
        foreach (var match in publishedMatches)
        {
            Assert.IsNotNull(match.Bot1Name);
            Assert.IsNotNull(match.Bot2Name);
            Assert.IsTrue(Enum.IsDefined(typeof(MatchOutcome), match.Outcome));
        }
    }

    [TestMethod]
    public async Task Hub_StateManager_MaintainsRecentMatchesFeed()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(6);
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, GameType.RPSLS, _baseConfig);

        var stateDto = ConvertToTournamentStateDto(tournamentInfo);
        stateDto.RecentMatches = tournamentInfo.MatchResults
            .OrderByDescending(m => tournamentInfo.MatchResults.IndexOf(m))
            .Take(10)
            .Select(ConvertToRecentMatchDto)
            .ToList();

        // Act - Publish via REAL Hub
        await _hubConnection.InvokeAsync("PublishStateUpdate", stateDto);
        await Task.Delay(100);

        // Assert
        var recentMatches = _stateManager.GetRecentMatches(10);
        Assert.IsNotNull(recentMatches);
        Assert.IsTrue(recentMatches.Count <= 10);
    }

    [TestMethod]
    public async Task Hub_SeriesProgress_TracksCompletedTournaments()
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

        // Act
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        var finalStateDto = new TournamentStateDto
        {
            TournamentId = Guid.NewGuid().ToString(),
            Status = TournamentStatus.Completed,
            Message = "Series completed",
            SeriesProgress = new SeriesProgressDto
            {
                SeriesId = Guid.NewGuid().ToString(),
                CompletedCount = seriesInfo.Tournaments.Count,
                TotalCount = seriesInfo.Tournaments.Count,
                CurrentTournamentIndex = seriesInfo.Tournaments.Count - 1,
                Tournaments = seriesInfo.Tournaments.Select((t, idx) => new TournamentInSeriesDto
                {
                    TournamentNumber = idx + 1,
                    GameType = t.GameType,
                    Status = TournamentItemStatus.Completed,
                    Champion = t.Champion
                }).ToList()
            },
            OverallLeaderboard = ConvertSeriesStandingsToTeamStandings(seriesInfo.SeriesStandings),
            LastUpdated = DateTime.UtcNow
        };

        await _hubConnection.InvokeAsync("PublishStateUpdate", finalStateDto);
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(4, finalStateDto.SeriesProgress.CompletedCount);
        Assert.AreEqual(4, finalStateDto.SeriesProgress.Tournaments.Count);
        Assert.IsTrue(finalStateDto.SeriesProgress.Tournaments.All(t => t.Status == TournamentItemStatus.Completed));
        Assert.IsTrue(finalStateDto.OverallLeaderboard.Count > 0);
        
        var state = await _stateManager.GetCurrentStateAsync();
        Assert.IsNotNull(state.SeriesProgress);
        Assert.AreEqual(4, state.SeriesProgress.CompletedCount);
    }

    [TestMethod]
    public async Task Hub_LeaderboardUpdates_ReflectSeriesStandings()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(10);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto },
            BaseConfig = _baseConfig
        };

        // Act
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        var stateDto = new TournamentStateDto
        {
            TournamentId = Guid.NewGuid().ToString(),
            Status = TournamentStatus.Completed,
            Message = "Leaderboard updated",
            OverallLeaderboard = ConvertSeriesStandingsToTeamStandings(seriesInfo.SeriesStandings),
            LastUpdated = DateTime.UtcNow
        };

        await _hubConnection.InvokeAsync("PublishStateUpdate", stateDto);
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(bots.Count, stateDto.OverallLeaderboard.Count);
        
        var topBot = stateDto.OverallLeaderboard.First();
        Assert.AreEqual(1, topBot.Rank);
        Assert.IsTrue(topBot.TotalPoints >= 0);
        
        // Verify rankings are properly ordered
        for (int i = 0; i < stateDto.OverallLeaderboard.Count - 1; i++)
        {
            Assert.IsTrue(stateDto.OverallLeaderboard[i].TotalPoints >= 
                          stateDto.OverallLeaderboard[i + 1].TotalPoints);
        }
    }

    [TestMethod]
    public async Task Hub_TournamentProgression_UpdatesCurrentTournamentInfo()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(6);
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, GameType.RPSLS, _baseConfig);

        var rankings = _scoringSystem.GetCurrentRankings(tournamentInfo);

        var stateDto = new TournamentStateDto
        {
            TournamentId = Guid.NewGuid().ToString(),
            Status = TournamentStatus.Completed,
            Message = $"Tournament completed - Champion: {tournamentInfo.Champion}",
            CurrentTournament = new CurrentTournamentDto
            {
                TournamentNumber = 1,
                GameType = tournamentInfo.GameType,
                Stage = TournamentStage.GroupStage,
                CurrentRound = tournamentInfo.CurrentRound,
                TotalRounds = tournamentInfo.TotalRounds,
                MatchesCompleted = tournamentInfo.MatchResults.Count,
                TotalMatches = tournamentInfo.MatchResults.Count,
                ProgressPercentage = 100.0
            },
            OverallLeaderboard = rankings.Select(r => new TeamStandingDto
            {
                Rank = r.FinalPlacement,
                TeamName = r.BotName,
                TotalPoints = r.TotalScore,
                TotalWins = r.Wins,
                TotalLosses = r.Losses
            }).ToList(),
            LastUpdated = DateTime.UtcNow
        };

        // Act - Publish via REAL Hub
        await _hubConnection.InvokeAsync("PublishStateUpdate", stateDto);
        await Task.Delay(100);

        // Assert
        var currentState = await _stateManager.GetCurrentStateAsync();
        Assert.IsNotNull(currentState.CurrentTournament);
        Assert.AreEqual(100.0, currentState.CurrentTournament.ProgressPercentage);
        Assert.AreEqual(TournamentStage.GroupStage, currentState.CurrentTournament.Stage);
    }

    [TestMethod]
    public async Task Hub_LargeSeriesIntegration_HandlesMultipleTournamentUpdates()
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

        // Act
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        // Publish state update for each completed tournament
        for (int i = 0; i < seriesInfo.Tournaments.Count; i++)
        {
            var tournament = seriesInfo.Tournaments[i];
            var rankings = _scoringSystem.GetCurrentRankings(tournament);
            
            var stateDto = new TournamentStateDto
            {
                TournamentId = Guid.NewGuid().ToString(),
                Status = i < seriesInfo.Tournaments.Count - 1 
                    ? TournamentStatus.InProgress 
                    : TournamentStatus.Completed,
                Message = $"Tournament {i + 1}/{seriesInfo.Tournaments.Count} completed",
                CurrentTournament = new CurrentTournamentDto
                {
                    TournamentNumber = i + 1,
                    GameType = tournament.GameType,
                    Stage = TournamentStage.GroupStage,
                    MatchesCompleted = tournament.MatchResults.Count,
                    TotalMatches = tournament.MatchResults.Count,
                    ProgressPercentage = 100.0
                },
                SeriesProgress = new SeriesProgressDto
                {
                    SeriesId = Guid.NewGuid().ToString(),
                    CompletedCount = i + 1,
                    TotalCount = seriesInfo.Tournaments.Count,
                    CurrentTournamentIndex = i
                },
                OverallLeaderboard = ConvertSeriesStandingsToTeamStandings(seriesInfo.SeriesStandings),
                LastUpdated = DateTime.UtcNow
            };
            
            await _hubConnection.InvokeAsync("PublishStateUpdate", stateDto);
            
            // Also publish some match completed events via REAL Hub
            foreach (var match in tournament.MatchResults.Take(5))
            {
                await _hubConnection.InvokeAsync("PublishMatchCompleted", ConvertToRecentMatchDto(match));
            }
        }
        await Task.Delay(200);

        // Assert
        var finalState = await _stateManager.GetCurrentStateAsync();
        Assert.IsNotNull(finalState);
        Assert.AreEqual(TournamentStatus.Completed, finalState.Status);
        Assert.IsNotNull(finalState.SeriesProgress);
        Assert.AreEqual(3, finalState.SeriesProgress.CompletedCount);
        Assert.IsTrue(finalState.OverallLeaderboard.Count > 0);
    }

    // Helper methods to convert tournament data to DTOs

    private TournamentStateDto ConvertToTournamentStateDto(TournamentInfo tournamentInfo)
    {
        var rankings = _scoringSystem.GetCurrentRankings(tournamentInfo);
        
        return new TournamentStateDto
        {
            TournamentId = Guid.NewGuid().ToString(),
            Status = tournamentInfo.State == TournamentState.Completed 
                ? TournamentStatus.Completed 
                : TournamentStatus.InProgress,
            Message = $"Tournament {tournamentInfo.State}",
            CurrentTournament = new CurrentTournamentDto
            {
                TournamentNumber = 1,
                GameType = tournamentInfo.GameType,
                Stage = TournamentStage.GroupStage,
                CurrentRound = tournamentInfo.CurrentRound,
                TotalRounds = tournamentInfo.TotalRounds,
                MatchesCompleted = tournamentInfo.MatchResults.Count,
                TotalMatches = tournamentInfo.MatchResults.Count,
                ProgressPercentage = tournamentInfo.State == TournamentState.Completed ? 100.0 : 50.0
            },
            OverallLeaderboard = rankings.Select(r => new TeamStandingDto
            {
                Rank = r.FinalPlacement,
                TeamName = r.BotName,
                TotalPoints = r.TotalScore,
                TotalWins = r.Wins,
                TotalLosses = r.Losses
            }).ToList(),
            RecentMatches = tournamentInfo.MatchResults
                .OrderByDescending(m => tournamentInfo.MatchResults.IndexOf(m))
                .Take(10)
                .Select(ConvertToRecentMatchDto)
                .ToList(),
            LastUpdated = DateTime.UtcNow
        };
    }

    private TournamentStateDto ConvertToTournamentStateDto(
        TournamentInfo tournamentInfo, 
        TournamentSeriesInfo seriesInfo, 
        int tournamentIndex)
    {
        var stateDto = ConvertToTournamentStateDto(tournamentInfo);
        
        stateDto.SeriesProgress = new SeriesProgressDto
        {
            SeriesId = Guid.NewGuid().ToString(),
            CompletedCount = tournamentIndex + 1,
            TotalCount = seriesInfo.Tournaments.Count,
            CurrentTournamentIndex = tournamentIndex,
            Tournaments = seriesInfo.Tournaments.Select((t, idx) => new TournamentInSeriesDto
            {
                TournamentNumber = idx + 1,
                GameType = t.GameType,
                Status = idx <= tournamentIndex 
                    ? TournamentItemStatus.Completed 
                    : TournamentItemStatus.Pending,
                Champion = idx <= tournamentIndex ? t.Champion : null
            }).ToList()
        };
        
        stateDto.OverallLeaderboard = ConvertSeriesStandingsToTeamStandings(seriesInfo.SeriesStandings);
        
        return stateDto;
    }

    private RecentMatchDto ConvertToRecentMatchDto(MatchResult match)
    {
        return new RecentMatchDto
        {
            MatchId = Guid.NewGuid().ToString(),
            Bot1Name = match.Bot1Name,
            Bot2Name = match.Bot2Name,
            Outcome = match.Outcome,
            WinnerName = match.Outcome == MatchOutcome.Player1Wins ? match.Bot1Name 
                       : match.Outcome == MatchOutcome.Player2Wins ? match.Bot2Name 
                       : null,
            Bot1Score = match.Bot1Score,
            Bot2Score = match.Bot2Score,
            CompletedAt = DateTime.UtcNow,
            GameType = match.GameType
        };
    }

    private List<TeamStandingDto> ConvertSeriesStandingsToTeamStandings(
        List<SeriesStanding> standings)
    {
        return standings
            .OrderByDescending(s => s.TotalSeriesScore)
            .Select((s, idx) => new TeamStandingDto
            {
                Rank = idx + 1,
                TeamName = s.BotName,
                TotalPoints = s.TotalSeriesScore,
                TournamentWins = s.TournamentPlacements.Count(p => p == 1),
                TotalWins = s.TotalWins,
                TotalLosses = s.TotalLosses
            })
            .ToList();
    }
}

