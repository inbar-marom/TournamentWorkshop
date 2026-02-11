using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using Xunit;

namespace TournamentEngine.Dashboard.Tests;

/// <summary>
/// Integration tests that verify multiple Dashboard services working together
/// Tests service interactions and end-to-end scenarios within the dashboard
/// Does NOT integrate with tournament engine logic yet
/// </summary>
public class DashboardIntegrationTests
{
    private readonly StateManagerService _stateManager;
    private readonly MatchFeedService _matchFeed;
    private readonly ExportService _exportService;
    private readonly ShareService _shareService;
    private readonly ThemeService _themeService;
    private readonly NotificationPreferencesService _notificationPrefs;
    private readonly ResponsiveLayoutService _layoutService;

    public DashboardIntegrationTests()
    {
        // Create all services with real instances (not mocks) to test integration
        _stateManager = new StateManagerService(Mock.Of<ILogger<StateManagerService>>());
        _matchFeed = new MatchFeedService(_stateManager);
        _exportService = new ExportService(_stateManager, _matchFeed, Mock.Of<ILogger<ExportService>>());
        _shareService = new ShareService(_stateManager, Mock.Of<ILogger<ShareService>>());
        _themeService = new ThemeService(Mock.Of<ILogger<ThemeService>>());
        _notificationPrefs = new NotificationPreferencesService(Mock.Of<ILogger<NotificationPreferencesService>>());
        _layoutService = new ResponsiveLayoutService(Mock.Of<ILogger<ResponsiveLayoutService>>());
    }

    #region State Management + Export Integration Tests

    [Fact]
    public async Task StateManager_And_ExportService_Integration_ExportsCurrentState()
    {
        // Arrange: Set up state with tournament data
        var state = await _stateManager.GetCurrentStateAsync();
        state.TournamentId = "test-tournament-123";
        state.OverallLeaderboard.Add(new TeamStandingDto
        {
            Rank = 1,
            TeamName = "TeamAlpha",
            TotalPoints = 100,
            TotalWins = 10,
            TotalLosses = 2
        });
        await _stateManager.UpdateStateAsync(state);

        // Act: Export the state
        var result = await _exportService.ExportAsync("json");

        // Assert: Exported data contains state information
        result.Format.Should().Be("json");
        result.Content.Should().Contain("TeamAlpha");
        result.Content.Should().Contain("100");
        result.FileName.Should().StartWith("tournament-");
        result.FileName.Should().EndWith(".json");
    }

    [Fact]
    public async Task StateManager_And_MatchFeed_Integration_AccumulatesMatchHistory()
    {
        // Arrange: Update state with matches
        var state = await _stateManager.GetCurrentStateAsync();
        state.RecentMatches.Add(new RecentMatchDto
        {
            MatchId = "match-1",
            Bot1Name = "TeamA",
            Bot2Name = "TeamB",
            WinnerName = "TeamA",
            CompletedAt = DateTime.UtcNow.AddMinutes(-5),
            GameType = GameType.RPSLS
        });
        state.RecentMatches.Add(new RecentMatchDto
        {
            MatchId = "match-2",
            Bot1Name = "TeamC",
            Bot2Name = "TeamD",
            WinnerName = "TeamC",
            CompletedAt = DateTime.UtcNow.AddMinutes(-3),
            GameType = GameType.RPSLS
        });
        await _stateManager.UpdateStateAsync(state);

        // Act: Get matches through feed service and export
        var matches = await _matchFeed.GetRecentMatchesAsync(10);
        var exportResult = await _exportService.ExportMatchHistoryAsync();

        // Assert: Export contains all matches
        matches.Should().HaveCount(2);
        exportResult.Should().Contain("TeamA");
        exportResult.Should().Contain("TeamB");
        exportResult.Should().Contain("TeamC");
        exportResult.Should().Contain("TeamD");
        exportResult.Should().Contain("match-1");
        exportResult.Should().Contain("match-2");
    }

    [Fact]
    public async Task ExportService_CsvFormat_ReflectsCurrentStandings()
    {
        // Arrange: Set up standings
        var state = await _stateManager.GetCurrentStateAsync();
        state.OverallLeaderboard.Add(new TeamStandingDto
        {
            Rank = 1,
            TeamName = "TeamAlpha",
            TotalPoints = 150,
            TotalWins = 15,
            TotalLosses = 3
        });
        state.OverallLeaderboard.Add(new TeamStandingDto
        {
            Rank = 2,
            TeamName = "TeamBeta",
            TotalPoints = 140,
            TotalWins = 14,
            TotalLosses = 4
        });
        await _stateManager.UpdateStateAsync(state);

        // Act: Export to CSV
        var result = await _exportService.ExportToCsvAsync();

        // Assert: CSV contains standings data
        result.Should().Contain("Rank,TeamName,Points,Wins,Losses");
        result.Should().Contain("1,TeamAlpha,150,15,3");
        result.Should().Contain("2,TeamBeta,140,14,4");
    }

    #endregion

    #region State Management + Share Service Integration Tests

    [Fact]
    public async Task ShareService_CreatesSnapshot_FromCurrentState()
    {
        // Arrange: Set up state
        var state = await _stateManager.GetCurrentStateAsync();
        state.TournamentId = "tournament-456";
        state.OverallLeaderboard.Add(new TeamStandingDto
        {
            Rank = 1,
            TeamName = "SnapshotTeam",
            TotalPoints = 200
        });
        await _stateManager.UpdateStateAsync(state);

        // Act: Create snapshot
        var snapshot = await _shareService.CreateSnapshotAsync();

        // Assert: Snapshot captures current state
        snapshot.Should().NotBeNull();
        snapshot.Id.Should().NotBeNullOrEmpty();
        snapshot.State.TournamentId.Should().Be("tournament-456");
        snapshot.State.OverallLeaderboard.Should().HaveCount(1);
        snapshot.State.OverallLeaderboard[0].TeamName.Should().Be("SnapshotTeam");
    }

    [Fact]
    public async Task ShareService_RetrievesSnapshot_IndependentOfCurrentState()
    {
        // Arrange: Create snapshot with initial state
        var initialState = await _stateManager.GetCurrentStateAsync();
        initialState.TournamentId = "initial-tournament";
        initialState.OverallLeaderboard.Add(new TeamStandingDto
        {
            Rank = 1,
            TeamName = "InitialTeam",
            TotalPoints = 100
        });
        await _stateManager.UpdateStateAsync(initialState);
        
        var snapshot = await _shareService.CreateSnapshotAsync();
        var snapshotId = snapshot.Id;

        // Modify state after snapshot
        var newState = await _stateManager.GetCurrentStateAsync();
        newState.TournamentId = "new-tournament";
        newState.OverallLeaderboard.Clear();
        newState.OverallLeaderboard.Add(new TeamStandingDto
        {
            Rank = 1,
            TeamName = "NewTeam",
            TotalPoints = 500
        });
        await _stateManager.UpdateStateAsync(newState);

        // Act: Retrieve original snapshot
        var retrievedSnapshot = await _shareService.GetSnapshotAsync(snapshotId);

        // Assert: Snapshot still has original data
        retrievedSnapshot.Should().NotBeNull();
        retrievedSnapshot!.State.TournamentId.Should().Be("initial-tournament");
        retrievedSnapshot.State.OverallLeaderboard.Should().HaveCount(1);
        retrievedSnapshot.State.OverallLeaderboard[0].TeamName.Should().Be("InitialTeam");
        retrievedSnapshot.State.OverallLeaderboard[0].TotalPoints.Should().Be(100);
    }

    [Fact]
    public async Task ShareService_GeneratesEmbedCode_WithSnapshotData()
    {
        // Arrange: Create snapshot
        var state = await _stateManager.GetCurrentStateAsync();
        state.TournamentId = "embed-test";
        await _stateManager.UpdateStateAsync(state);
        var snapshot = await _shareService.CreateSnapshotAsync();

        // Act: Generate embed code
        var embedCode = await _shareService.GenerateEmbedCodeAsync(snapshot.Id, 1024, 768);

        // Assert: Embed code includes snapshot ID
        embedCode.Should().Contain("iframe");
        embedCode.Should().Contain(snapshot.Id);
        embedCode.Should().Contain("1024");
        embedCode.Should().Contain("768");
    }

    #endregion

    #region User Preferences Integration Tests

    [Fact]
    public async Task ThemeAndNotificationPreferences_WorkTogether()
    {
        // Arrange: Set theme to dark mode
        await _themeService.SetThemeAsync("dark");
        
        // Set notification preferences
        await _notificationPrefs.SetSoundEffectsEnabledAsync(false);
        await _notificationPrefs.SetAnimationsEnabledAsync(true);

        // Act: Get both preferences
        var theme = await _themeService.GetCurrentThemeAsync();
        var notifPrefs = await _notificationPrefs.GetPreferencesAsync();

        // Assert: Both preferences are maintained independently
        theme.Mode.Should().Be("dark");
        notifPrefs.SoundEffectsEnabled.Should().BeFalse();
        notifPrefs.AnimationsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ResponsiveLayout_AdaptsToDevice_WithThemePreferences()
    {
        // Arrange: Set dark theme
        await _themeService.SetThemeAsync("dark");
        
        // Detect mobile device
        var deviceType = await _layoutService.DetectDeviceTypeAsync("Mozilla/5.0 (iPhone; CPU iPhone OS 14_0 like Mac OS X)");
        var layoutConfig = await _layoutService.GetLayoutConfigAsync(deviceType);

        // Act: Get theme while on mobile layout
        var theme = await _themeService.GetCurrentThemeAsync();

        // Assert: Both services work correctly
        deviceType.Should().Be("mobile");
        layoutConfig.ColumnCount.Should().Be(1);
        layoutConfig.ShowSidebar.Should().BeFalse();
        theme.Mode.Should().Be("dark");
    }

    [Fact]
    public async Task NotificationPreferences_ReducedMotion_DisablesAnimations()
    {
        // Arrange: Enable animations initially
        await _notificationPrefs.SetAnimationsEnabledAsync(true);

        // Act: Enable reduced motion (accessibility)
        await _notificationPrefs.SetReducedMotionAsync(true);
        var prefs = await _notificationPrefs.GetPreferencesAsync();

        // Assert: Animations are auto-disabled for accessibility
        prefs.ReducedMotion.Should().BeTrue();
        prefs.AnimationsEnabled.Should().BeFalse();
    }

    #endregion

    #region Full Dashboard Workflow Tests

    [Fact]
    public async Task FullDashboardWorkflow_NewTournament_ExportAndShare()
    {
        // Scenario: Start new tournament, add matches, export, and share

        // Step 1: Initialize tournament state
        var state = await _stateManager.GetCurrentStateAsync();
        state.TournamentId = "workflow-tournament";
        state.Status = TournamentStatus.InProgress;
        state.RecentMatches.Add(new RecentMatchDto
        {
            MatchId = "match-1",
            Bot1Name = "TeamA",
            Bot2Name = "TeamB",
            WinnerName = "TeamA",
            CompletedAt = DateTime.UtcNow,
            GameType = GameType.RPSLS
        });
        state.OverallLeaderboard.Add(new TeamStandingDto
        {
            Rank = 1,
            TeamName = "TeamA",
            TotalPoints = 10,
            TotalWins = 1,
            TotalLosses = 0
        });
        await _stateManager.UpdateStateAsync(state);

        // Step 2: Export tournament data
        var jsonExport = await _exportService.ExportAsync("json");
        var csvExport = await _exportService.ExportToCsvAsync();

        // Step 3: Create shareable snapshot
        var snapshot = await _shareService.CreateSnapshotAsync();
        var shareLink = await _shareService.GenerateShareLinkAsync("Check out this tournament!");

        // Assert: All operations completed successfully
        jsonExport.Content.Should().Contain("TeamA");
        csvExport.Should().Contain("TeamA");
        snapshot.State.TournamentId.Should().Be("workflow-tournament");
        shareLink.Should().Contain("http://localhost:5000/share");
        shareLink.Should().Contain("message=Check%20out%20this%20tournament%21"); // URL encoded
    }

    [Fact]
    public async Task FullDashboardWorkflow_UserPreferences_PersistAcrossOperations()
    {
        // Scenario: User sets preferences, then performs dashboard operations

        // Step 1: Set user preferences
        await _themeService.SetThemeAsync("dark");
        await _notificationPrefs.SetSoundVolumeAsync(0.8);
        await _notificationPrefs.SetAnimationSpeedAsync("fast");
        await _layoutService.SetPreferredLayoutAsync("compact");

        // Step 2: Perform dashboard operations
        var state = await _stateManager.GetCurrentStateAsync();
        state.TournamentId = "prefs-test";
        await _stateManager.UpdateStateAsync(state);
        
        var snapshot = await _shareService.CreateSnapshotAsync();

        // Step 3: Verify preferences persist
        var theme = await _themeService.GetCurrentThemeAsync();
        var notifPrefs = await _notificationPrefs.GetPreferencesAsync();

        // Assert: All preferences maintained
        theme.Mode.Should().Be("dark");
        notifPrefs.SoundVolume.Should().Be(0.8);
        notifPrefs.AnimationSpeed.Should().Be("fast");
        snapshot.Should().NotBeNull();
    }

[Fact]
    public async Task MultipleSnapshots_WithDifferentStates_MaintainIndependence()
    {
        // Step 1: Create first snapshot
        var state1 = await _stateManager.GetCurrentStateAsync();
        state1.TournamentId = "tournament-1";
        state1.OverallLeaderboard.Clear();
        state1.OverallLeaderboard.Add(new TeamStandingDto { Rank = 1, TeamName = "Team1", TotalPoints = 100 });
        await _stateManager.UpdateStateAsync(state1);
        var snapshot1 = await _shareService.CreateSnapshotAsync();

        // Step 2: Create second snapshot with different state
        var state2 = await _stateManager.GetCurrentStateAsync();
        state2.TournamentId = "tournament-2";
        state2.OverallLeaderboard.Clear();
        state2.OverallLeaderboard.Add(new TeamStandingDto { Rank = 1, TeamName = "Team2", TotalPoints = 200 });
        await _stateManager.UpdateStateAsync(state2);
        var snapshot2 = await _shareService.CreateSnapshotAsync();

        // Step 3: Create third snapshot
        var state3 = await _stateManager.GetCurrentStateAsync();
        state3.TournamentId = "tournament-3";
        state3.OverallLeaderboard.Clear();
        state3.OverallLeaderboard.Add(new TeamStandingDto { Rank = 1, TeamName = "Team3", TotalPoints = 300 });
        await _stateManager.UpdateStateAsync(state3);
        var snapshot3 = await _shareService.CreateSnapshotAsync();

        // Act: Retrieve all snapshots
        var retrieved1 = await _shareService.GetSnapshotAsync(snapshot1.Id);
        var retrieved2 = await _shareService.GetSnapshotAsync(snapshot2.Id);
        var retrieved3 = await _shareService.GetSnapshotAsync(snapshot3.Id);

        // Assert: Each snapshot maintains its original state
        retrieved1!.State.TournamentId.Should().Be("tournament-1");
        retrieved1.State.OverallLeaderboard[0].TeamName.Should().Be("Team1");
        retrieved1.State.OverallLeaderboard[0].TotalPoints.Should().Be(100);

        retrieved2!.State.TournamentId.Should().Be("tournament-2");
        retrieved2.State.OverallLeaderboard[0].TeamName.Should().Be("Team2");
        retrieved2.State.OverallLeaderboard[0].TotalPoints.Should().Be(200);

        retrieved3!.State.TournamentId.Should().Be("tournament-3");
        retrieved3.State.OverallLeaderboard[0].TeamName.Should().Be("Team3");
        retrieved3.State.OverallLeaderboard[0].TotalPoints.Should().Be(300);
    }

    [Fact]
    public async Task ExportFullSnapshot_IncludesAllDashboardData()
    {
        // Arrange: Set up complete dashboard state
        var state = await _stateManager.GetCurrentStateAsync();
        state.TournamentId = "full-export-test";
        state.Status = TournamentStatus.InProgress;
        state.RecentMatches.Add(new RecentMatchDto
        {
            MatchId = "full-match-1",
            Bot1Name = "CompleteTeam",
            Bot2Name = "OpponentTeam",
            WinnerName = "CompleteTeam",
            CompletedAt = DateTime.UtcNow,
            GameType = GameType.RPSLS
        });
        state.OverallLeaderboard.Add(new TeamStandingDto
        {
            Rank = 1,
            TeamName = "CompleteTeam",
            TotalPoints = 250,
            TotalWins = 20,
            TotalLosses = 5
        });
        await _stateManager.UpdateStateAsync(state);

        // Act: Export full snapshot
        var fullExport = await _exportService.ExportFullSnapshotAsync();

        // Assert: Export contains all data
        fullExport.Should().Contain("full-export-test");
        fullExport.Should().Contain("CompleteTeam");
        fullExport.Should().Contain("250");
        fullExport.Should().Contain("full-match-1");
        fullExport.Should().Contain("Version");
        fullExport.Should().Contain("ExportedAt");
    }

    [Fact]
    public async Task Dashboard_HandlesMultipleConcurrentOperations()
    {
        // Simulate concurrent dashboard operations
        var tasks = new List<Task>();

        // Concurrent state updates
        tasks.Add(Task.Run(async () =>
        {
            var state = await _stateManager.GetCurrentStateAsync();
            state.Status = TournamentStatus.InProgress;
            await _stateManager.UpdateStateAsync(state);
        }));

        // Concurrent snapshot creation
        tasks.Add(Task.Run(async () =>
        {
            await _shareService.CreateSnapshotAsync();
        }));

        // Concurrent export
        tasks.Add(Task.Run(async () =>
        {
            await _exportService.ExportToJsonAsync();
        }));

        // Concurrent theme changes
        tasks.Add(Task.Run(async () =>
        {
            await _themeService.ToggleThemeAsync();
        }));

        // Act: Await all concurrent operations
        await Task.WhenAll(tasks);

        // Assert: All operations completed without exceptions
        var finalState = await _stateManager.GetCurrentStateAsync();
        var snapshots = await _shareService.ListSnapshotsAsync();

        finalState.Should().NotBeNull();
        snapshots.Should().NotBeEmpty();
    }

    #endregion

    #region Edge Cases and Error Handling Integration Tests

    [Fact]
    public async Task ExportService_HandlesEmptyState_Gracefully()
    {
        // Arrange: Fresh state with no data
        var state = await _stateManager.GetCurrentStateAsync();
        state.OverallLeaderboard.Clear();
        state.RecentMatches.Clear();
        await _stateManager.UpdateStateAsync(state);

        // Act: Export empty state
        var jsonExport = await _exportService.ExportToJsonAsync();
        var csvExport = await _exportService.ExportToCsvAsync();

        // Assert: Exports complete without errors
        jsonExport.Should().NotBeNullOrEmpty();
        csvExport.Should().Contain("Rank,TeamName,Points,Wins,Losses");
    }

    [Fact]
    public async Task ShareService_TrackingViews_UpdatesStatsCorrectly()
    {
        // Arrange: Create snapshot
        var snapshot = await _shareService.CreateSnapshotAsync();

        // Act: Track multiple views
        await _shareService.TrackViewAsync(snapshot.Id);
        await _shareService.TrackViewAsync(snapshot.Id);
        await _shareService.TrackViewAsync(snapshot.Id);

        var stats = await _shareService.GetShareStatsAsync(snapshot.Id);

        // Assert: View count is correct
        stats.Should().NotBeNull();
        stats.ViewCount.Should().Be(3);
    }

    [Fact]
    public async Task ShareService_DeleteSnapshot_RemovesFromList()
    {
        // Arrange: Create multiple snapshots
        var snapshot1 = await _shareService.CreateSnapshotAsync();
        var snapshot2 = await _shareService.CreateSnapshotAsync();
        var snapshot3 = await _shareService.CreateSnapshotAsync();

        // Act: Delete one snapshot
        var deleteResult = await _shareService.DeleteSnapshotAsync(snapshot2.Id);

        var remainingSnapshots = await _shareService.ListSnapshotsAsync();

        // Assert: Snapshot removed, others remain
        deleteResult.Should().BeTrue();
        remainingSnapshots.Should().HaveCount(2);
        remainingSnapshots.Should().Contain(s => s.Id == snapshot1.Id);
        remainingSnapshots.Should().Contain(s => s.Id == snapshot3.Id);
        remainingSnapshots.Should().NotContain(s => s.Id == snapshot2.Id);
    }

    #endregion
}
