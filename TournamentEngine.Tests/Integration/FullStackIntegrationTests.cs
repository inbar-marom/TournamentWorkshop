namespace TournamentEngine.Tests.Integration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Dashboard.DTOs;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// Full-stack integration tests combining Tournament Engine (including Series) 
/// with Dashboard services to verify end-to-end system functionality.
/// </summary>
[TestClass]
public class FullStackIntegrationTests
{
    // Tournament Engine Stack
    private GameRunner _gameRunner = null!;
    private ScoringSystem _scoringSystem = null!;
    private GroupStageTournamentEngine _engine = null!;
    private TournamentManager _tournamentManager = null!;
    private TournamentSeriesManager _seriesManager = null!;
    
    // Dashboard Services Stack
    private StateManagerService _stateManager = null!;
    private MatchFeedService _matchFeed = null!;
    private ExportService _exportService = null!;
    private ShareService _shareService = null!;
    private ThemeService _themeService = null!;
    private LayoutService _layoutService = null!;
    private NotificationService _notificationService = null!;
    
    private TournamentConfig _baseConfig = null!;

    [TestInitialize]
    public void Setup()
    {
        // Initialize Tournament Engine Stack
        _baseConfig = IntegrationTestHelpers.CreateConfig();
        _gameRunner = new GameRunner(_baseConfig);
        _scoringSystem = new ScoringSystem();
        _engine = new GroupStageTournamentEngine(_gameRunner, _scoringSystem);
        _tournamentManager = new TournamentManager(_engine, _gameRunner);
        _seriesManager = new TournamentSeriesManager(_tournamentManager, _scoringSystem);
        
        // Initialize Dashboard Services Stack
        _stateManager = new StateManagerService();
        _matchFeed = new MatchFeedService();
        _exportService = new ExportService(_stateManager);
        _shareService = new ShareService(_stateManager);
        _themeService = new ThemeService();
        _layoutService = new LayoutService();
        _notificationService = new NotificationService();
    }

    [TestMethod]
    public async Task FullStack_SingleTournament_UpdatesDashboardInRealTime()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(8);
        
        // Act - Run tournament and publish to dashboard
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, 
            GameType.RPSLS, 
            _baseConfig);
        
        // Convert TournamentInfo to TournamentStateDto
        var state = ConvertToStateDto(tournamentInfo);
        await _stateManager.UpdateStateAsync(state);
        
        // Add matches to feed
        foreach (var match in tournamentInfo.MatchResults)
        {
            var matchDto = ConvertToRecentMatchDto(match);
            await _matchFeed.AddMatchAsync(matchDto);
        }
        
        // Assert - Tournament completed successfully
        Assert.AreEqual(TournamentState.Completed, tournamentInfo.State);
        Assert.IsNotNull(tournamentInfo.Champion);
        
        // Assert - Dashboard state matches tournament state
        var currentState = await _stateManager.GetCurrentStateAsync();
        Assert.IsNotNull(currentState);
        Assert.AreEqual(TournamentStatus.Completed, currentState.Status);
        Assert.AreEqual(tournamentInfo.Champion, currentState.CurrentChampion);
        Assert.AreEqual(tournamentInfo.MatchResults.Count, currentState.MatchesPlayed);
        
        // Assert - Match feed has all matches
        var recentMatches = await _matchFeed.GetRecentMatchesAsync(100);
        Assert.AreEqual(tournamentInfo.MatchResults.Count, recentMatches.Count);
        
        // Assert - Export functionality works with tournament data
        var jsonExport = await _exportService.ExportStateAsJsonAsync();
        Assert.IsTrue(jsonExport.Contains(tournamentInfo.Champion));
        
        var csvExport = await _exportService.ExportStandingsAsCsvAsync();
        Assert.IsTrue(csvExport.Contains(tournamentInfo.Champion));
    }

    [TestMethod]
    public async Task FullStack_TournamentSeries_AggregatesAcrossDashboard()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(10);
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
        
        // Act - Run series and update dashboard for each tournament
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        int totalMatchesProcessed = 0;
        foreach (var tournament in seriesInfo.Tournaments)
        {
            // Update dashboard state for this tournament
            var state = ConvertToStateDto(tournament);
            state.SeriesInfo = $"Tournament {seriesInfo.Tournaments.IndexOf(tournament) + 1} of {seriesInfo.Tournaments.Count}";
            await _stateManager.UpdateStateAsync(state);
            
            // Add matches to feed
            foreach (var match in tournament.MatchResults)
            {
                var matchDto = ConvertToRecentMatchDto(match);
                await _matchFeed.AddMatchAsync(matchDto);
                totalMatchesProcessed++;
            }
        }
        
        // Final series state
        var finalState = new TournamentStateDto
        {
            Status = TournamentStatus.Completed,
            StatusMessage = $"Series completed! Champion: {seriesInfo.SeriesChampion}",
            CurrentChampion = seriesInfo.SeriesChampion,
            MatchesPlayed = seriesInfo.TotalMatches,
            TotalParticipants = bots.Count,
            CurrentRound = seriesInfo.Tournaments.Count,
            TotalRounds = seriesInfo.Tournaments.Count,
            SeriesInfo = "Series Complete",
            Standings = ConvertSeriesStandings(seriesInfo.SeriesStandings)
        };
        await _stateManager.UpdateStateAsync(finalState);
        
        // Assert - Series completed successfully
        Assert.AreEqual(3, seriesInfo.Tournaments.Count);
        Assert.IsNotNull(seriesInfo.SeriesChampion);
        
        // Assert - Dashboard shows final series state
        var currentState = await _stateManager.GetCurrentStateAsync();
        Assert.AreEqual(TournamentStatus.Completed, currentState.Status);
        Assert.AreEqual(seriesInfo.SeriesChampion, currentState.CurrentChampion);
        Assert.AreEqual(seriesInfo.TotalMatches, currentState.MatchesPlayed);
        Assert.AreEqual(bots.Count, currentState.Standings.Count);
        
        // Assert - All matches recorded in feed
        var allMatches = await _matchFeed.GetRecentMatchesAsync(1000);
        Assert.AreEqual(totalMatchesProcessed, allMatches.Count);
        
        // Assert - Can export series results
        var jsonExport = await _exportService.ExportStateAsJsonAsync();
        Assert.IsTrue(jsonExport.Contains(seriesInfo.SeriesChampion));
        Assert.IsTrue(jsonExport.Contains("Series Complete"));
    }

    [TestMethod]
    public async Task FullStack_Tournament_WithDashboardSnapshot_PreservesState()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(8);
        
        // Act - Run tournament with intermediate snapshots
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, 
            GameType.RPSLS, 
            _baseConfig);
        
        // Simulate mid-tournament state (50% matches complete)
        int midPoint = tournamentInfo.MatchResults.Count / 2;
        var midMatches = tournamentInfo.MatchResults.Take(midPoint).ToList();
        
        var midState = new TournamentStateDto
        {
            Status = TournamentStatus.InProgress,
            StatusMessage = $"Tournament in progress - {midPoint} matches completed",
            MatchesPlayed = midPoint,
            TotalParticipants = bots.Count,
            CurrentRound = 1,
            TotalRounds = 2,
            Standings = new List<StandingDto>()
        };
        await _stateManager.UpdateStateAsync(midState);
        
        // Create snapshot at mid-point
        var midSnapshot = await _shareService.CreateSnapshotAsync("Mid-Tournament");
        
        // Complete tournament
        var finalState = ConvertToStateDto(tournamentInfo);
        await _stateManager.UpdateStateAsync(finalState);
        
        // Create final snapshot
        var finalSnapshot = await _shareService.CreateSnapshotAsync("Final Results");
        
        // Assert - Both snapshots preserved independently
        var retrievedMidSnapshot = await _shareService.GetSnapshotAsync(midSnapshot.SnapshotId);
        var retrievedFinalSnapshot = await _shareService.GetSnapshotAsync(finalSnapshot.SnapshotId);
        
        Assert.IsNotNull(retrievedMidSnapshot);
        Assert.IsNotNull(retrievedFinalSnapshot);
        
        Assert.AreEqual(TournamentStatus.InProgress, retrievedMidSnapshot.State.Status);
        Assert.AreEqual(TournamentStatus.Completed, retrievedFinalSnapshot.State.Status);
        
        Assert.AreEqual(midPoint, retrievedMidSnapshot.State.MatchesPlayed);
        Assert.AreEqual(tournamentInfo.MatchResults.Count, retrievedFinalSnapshot.State.MatchesPlayed);
        
        // Assert - Snapshots are independent (modifying current state doesn't affect snapshots)
        await _stateManager.UpdateStateAsync(new TournamentStateDto
        {
            Status = TournamentStatus.NotStarted,
            StatusMessage = "Reset"
        });
        
        var stillMidSnapshot = await _shareService.GetSnapshotAsync(midSnapshot.SnapshotId);
        Assert.AreEqual(TournamentStatus.InProgress, stillMidSnapshot.State.Status);
    }

    [TestMethod]
    public async Task FullStack_Series_WithUserPreferences_PersistsAcrossTournaments()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(5);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto },
            BaseConfig = _baseConfig
        };
        
        // Set user preferences before series
        var userId = "test-user";
        await _themeService.SetThemeAsync(userId, "dark");
        await _layoutService.SetLayoutAsync(userId, "compact");
        await _notificationService.SetNotificationPreferenceAsync(userId, "match-complete", true);
        await _notificationService.SetNotificationPreferenceAsync(userId, "round-complete", true);
        
        // Act - Run series
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        foreach (var tournament in seriesInfo.Tournaments)
        {
            var state = ConvertToStateDto(tournament);
            await _stateManager.UpdateStateAsync(state);
            
            // Simulate notifications
            await _notificationService.AddNotificationAsync(userId, 
                $"Tournament {tournament.GameType} completed!");
        }
        
        // Assert - Series completed
        Assert.AreEqual(2, seriesInfo.Tournaments.Count);
        
        // Assert - User preferences persisted
        var theme = await _themeService.GetThemeAsync(userId);
        var layout = await _layoutService.GetLayoutAsync(userId);
        var matchNotifs = await _notificationService.GetNotificationPreferenceAsync(userId, "match-complete");
        var roundNotifs = await _notificationService.GetNotificationPreferenceAsync(userId, "round-complete");
        
        Assert.AreEqual("dark", theme);
        Assert.AreEqual("compact", layout);
        Assert.IsTrue(matchNotifs);
        Assert.IsTrue(roundNotifs);
        
        // Assert - Notifications received
        var notifications = await _notificationService.GetNotificationsAsync(userId, 10);
        Assert.AreEqual(2, notifications.Count); // One per tournament
    }

    [TestMethod]
    public async Task FullStack_LargeSeries_StressTestDashboardServices()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(20);
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
        
        // Act - Run large series
        var startTime = DateTime.UtcNow;
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        var endTime = DateTime.UtcNow;
        
        // Process all matches through dashboard
        int matchCount = 0;
        foreach (var tournament in seriesInfo.Tournaments)
        {
            var state = ConvertToStateDto(tournament);
            await _stateManager.UpdateStateAsync(state);
            
            foreach (var match in tournament.MatchResults)
            {
                var matchDto = ConvertToRecentMatchDto(match);
                await _matchFeed.AddMatchAsync(matchDto);
                matchCount++;
            }
        }
        
        // Create snapshot
        var finalState = ConvertToStateDto(seriesInfo.Tournaments.Last());
        finalState.StatusMessage = $"Series completed! Champion: {seriesInfo.SeriesChampion}";
        finalState.CurrentChampion = seriesInfo.SeriesChampion;
        await _stateManager.UpdateStateAsync(finalState);
        
        var snapshot = await _shareService.CreateSnapshotAsync("Large Series Results");
        
        // Assert - Series completed successfully
        Assert.AreEqual(4, seriesInfo.Tournaments.Count);
        Assert.IsTrue(seriesInfo.TotalMatches > 0);
        
        // Assert - Dashboard handled all data
        var currentState = await _stateManager.GetCurrentStateAsync();
        Assert.IsNotNull(currentState);
        
        var recentMatches = await _matchFeed.GetRecentMatchesAsync(100);
        Assert.IsTrue(recentMatches.Count > 0);
        
        // Assert - Snapshot captured final state
        var retrievedSnapshot = await _shareService.GetSnapshotAsync(snapshot.SnapshotId);
        Assert.IsNotNull(retrievedSnapshot);
        Assert.IsTrue(retrievedSnapshot.State.StatusMessage.Contains(seriesInfo.SeriesChampion));
        
        // Assert - Export works with large dataset
        var jsonExport = await _exportService.ExportStateAsJsonAsync();
        Assert.IsTrue(jsonExport.Length > 0);
        
        var csvExport = await _exportService.ExportStandingsAsCsvAsync();
        var csvLines = csvExport.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.IsTrue(csvLines.Length > bots.Count); // Header + all bots
        
        // Performance assertion - should complete in reasonable time
        var duration = endTime - startTime;
        Assert.IsTrue(duration.TotalMinutes < 5, 
            $"Series took {duration.TotalMinutes:F2} minutes - should be under 5 minutes");
    }

    [TestMethod]
    public async Task FullStack_Tournament_ExportAndShare_ProduceConsistentResults()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(10);
        
        // Act - Run tournament
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, 
            GameType.RPSLS, 
            _baseConfig);
        
        var state = ConvertToStateDto(tournamentInfo);
        await _stateManager.UpdateStateAsync(state);
        
        // Export in different formats
        var jsonExport = await _exportService.ExportStateAsJsonAsync();
        var csvExport = await _exportService.ExportStandingsAsCsvAsync();
        
        // Create snapshot
        var snapshot = await _shareService.CreateSnapshotAsync("Tournament Results");
        var retrievedSnapshot = await _shareService.GetSnapshotAsync(snapshot.SnapshotId);
        
        // Assert - All formats contain champion
        Assert.IsTrue(jsonExport.Contains(tournamentInfo.Champion));
        Assert.IsTrue(csvExport.Contains(tournamentInfo.Champion));
        Assert.AreEqual(tournamentInfo.Champion, retrievedSnapshot.State.CurrentChampion);
        
        // Assert - Match counts consistent
        Assert.AreEqual(tournamentInfo.MatchResults.Count, state.MatchesPlayed);
        Assert.AreEqual(tournamentInfo.MatchResults.Count, retrievedSnapshot.State.MatchesPlayed);
        
        // Assert - Participant counts consistent
        Assert.AreEqual(bots.Count, state.TotalParticipants);
        Assert.AreEqual(bots.Count, retrievedSnapshot.State.TotalParticipants);
        Assert.AreEqual(bots.Count, state.Standings.Count);
        
        // Assert - JSON contains all necessary fields
        Assert.IsTrue(jsonExport.Contains("\"Status\""));
        Assert.IsTrue(jsonExport.Contains("\"CurrentChampion\""));
        Assert.IsTrue(jsonExport.Contains("\"Standings\""));
        
        // Assert - CSV has proper structure
        var csvLines = csvExport.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.IsTrue(csvLines.Length > 1); // Header + data
        Assert.IsTrue(csvLines[0].Contains("Rank")); // Header validation
    }

    [TestMethod]
    public async Task FullStack_SeriesWithSnapshots_TracksProgressOverTime()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(8);
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
        
        // Act - Run series with snapshot after each tournament
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        var snapshots = new List<ShareLinkDto>();
        for (int i = 0; i < seriesInfo.Tournaments.Count; i++)
        {
            var tournament = seriesInfo.Tournaments[i];
            var state = ConvertToStateDto(tournament);
            state.SeriesInfo = $"Tournament {i + 1} of {seriesInfo.Tournaments.Count}";
            await _stateManager.UpdateStateAsync(state);
            
            var snapshot = await _shareService.CreateSnapshotAsync(
                $"{tournament.GameType} Results");
            snapshots.Add(snapshot);
        }
        
        // Assert - All snapshots created
        Assert.AreEqual(3, snapshots.Count);
        
        // Assert - Each snapshot is independent
        var snapshot1 = await _shareService.GetSnapshotAsync(snapshots[0].SnapshotId);
        var snapshot2 = await _shareService.GetSnapshotAsync(snapshots[1].SnapshotId);
        var snapshot3 = await _shareService.GetSnapshotAsync(snapshots[2].SnapshotId);
        
        Assert.IsTrue(snapshot1.State.SeriesInfo.Contains("Tournament 1 of 3"));
        Assert.IsTrue(snapshot2.State.SeriesInfo.Contains("Tournament 2 of 3"));
        Assert.IsTrue(snapshot3.State.SeriesInfo.Contains("Tournament 3 of 3"));
        
        // Assert - Can retrieve all snapshots
        var allLinks = await _shareService.GetAllShareLinksAsync();
        Assert.AreEqual(3, allLinks.Count);
        
        foreach (var link in allLinks)
        {
            Assert.IsTrue(link.ViewCount >= 0);
            Assert.IsNotNull(link.CreatedAt);
        }
    }

    #region Helper Methods

    private TournamentStateDto ConvertToStateDto(TournamentInfo tournamentInfo)
    {
        var rankings = _scoringSystem.GetCurrentRankings(tournamentInfo);
        
        return new TournamentStateDto
        {
            Status = tournamentInfo.State == TournamentState.Completed 
                ? TournamentStatus.Completed 
                : TournamentStatus.InProgress,
            StatusMessage = tournamentInfo.State == TournamentState.Completed
                ? $"Tournament completed! Champion: {tournamentInfo.Champion}"
                : "Tournament in progress",
            CurrentChampion = tournamentInfo.Champion,
            MatchesPlayed = tournamentInfo.MatchResults.Count,
            TotalParticipants = tournamentInfo.Bots.Count,
            CurrentRound = 1,
            TotalRounds = 1,
            Standings = rankings.Select(r => new StandingDto
            {
                Rank = r.FinalPlacement,
                TeamName = r.BotName,
                Points = r.TotalScore,
                Wins = r.Wins,
                Losses = r.Losses,
                Draws = r.Draws
            }).ToList()
        };
    }

    private RecentMatchDto ConvertToRecentMatchDto(MatchResult match)
    {
        return new RecentMatchDto
        {
            Team1 = match.Bot1Name,
            Team2 = match.Bot2Name,
            Score1 = match.Bot1Score,
            Score2 = match.Bot2Score,
            Winner = match.Outcome == MatchOutcome.Player1Wins ? match.Bot1Name
                   : match.Outcome == MatchOutcome.Player2Wins ? match.Bot2Name
                   : "Draw",
            Timestamp = DateTime.UtcNow
        };
    }

    private List<StandingDto> ConvertSeriesStandings(List<SeriesStanding> seriesStandings)
    {
        return seriesStandings
            .OrderByDescending(s => s.TotalSeriesScore)
            .Select((s, index) => new StandingDto
            {
                Rank = index + 1,
                TeamName = s.BotName,
                Points = s.TotalSeriesScore,
                Wins = s.TotalWins,
                Losses = s.TotalLosses,
                Draws = 0 // Series standings don't track draws separately
            })
            .ToList();
    }

    #endregion
}
