using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for providing tournament visualization data (brackets, trees, progressions).
/// </summary>
public class TournamentVisualizationService
{
    private readonly StateManagerService _stateManager;
    private readonly MatchFeedService _matchFeed;

    public TournamentVisualizationService(StateManagerService stateManager, MatchFeedService matchFeed)
    {
        _stateManager = stateManager;
        _matchFeed = matchFeed;
    }

    /// <summary>
    /// Gets bracket visualization data for the current tournament.
    /// </summary>
    public async Task<BracketVisualizationDto?> GetBracketVisualizationAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        if (state?.CurrentTournament == null)
            return null;

        var bracket = new BracketVisualizationDto();
        var matches = state.RecentMatches ?? new List<RecentMatchDto>();

        if (matches.Count == 0)
            return bracket;

        // Group matches by round (simplified: assume recent matches are final rounds)
        var roundMatches = matches.Take(Math.Min(matches.Count, 8)).ToList();

        var round = new BracketRoundDto { RoundNumber = 1 };
        foreach (var match in roundMatches)
        {
            round.Matchups.Add(new BracketMatchupDto
            {
                Team1 = match.Bot1Name,
                Team2 = match.Bot2Name,
                Winner = match.WinnerName
            });
        }

        bracket.Rounds.Add(round);
        return bracket;
    }

    /// <summary>
    /// Gets tournament tree structure showing stages and progression.
    /// </summary>
    public async Task<TournamentTreeDto> GetTournamentTreeStructureAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        var tree = new TournamentTreeDto();

        if (state?.CurrentTournament == null)
            return tree;

        tree.CurrentStage = state.CurrentTournament.Stage.ToString();
        tree.CurrentRound = state.CurrentTournament.CurrentRound;
        tree.TotalRounds = state.CurrentTournament.TotalRounds;

        // Add stages
        tree.Stages.Add("GroupStage");
        tree.Stages.Add("Finals");

        return tree;
    }

    /// <summary>
    /// Gets round-robin grid showing matchups between all teams.
    /// </summary>
    public async Task<RoundRobinGridDto> GetRoundRobinGridAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        var matches = await _matchFeed.GetRecentMatchesAsync(1000);
        var grid = new RoundRobinGridDto();

        if (state?.OverallLeaderboard == null || state.OverallLeaderboard.Count == 0)
            return grid;

        // Get unique team names
        var teams = state.OverallLeaderboard.Select(t => t.TeamName).ToList();
        grid.TeamNames = teams;

        // Create results matrix
        var matrix = new Dictionary<string, Dictionary<string, string>>();
        foreach (var team in teams)
        {
            matrix[team] = new Dictionary<string, string>();
            foreach (var opponent in teams)
            {
                if (team == opponent)
                {
                    matrix[team][opponent] = "-";
                }
                else
                {
                    var match = matches.FirstOrDefault(m =>
                        (m.Bot1Name == team && m.Bot2Name == opponent) ||
                        (m.Bot1Name == opponent && m.Bot2Name == team));

                    if (match == null)
                        matrix[team][opponent] = "";
                    else if (match.WinnerName == team)
                        matrix[team][opponent] = "W";
                    else if (match.Outcome == MatchOutcome.Draw)
                        matrix[team][opponent] = "D";
                    else
                        matrix[team][opponent] = "L";
                }
            }
        }

        grid.ResultsMatrix = matrix;
        return grid;
    }

    /// <summary>
    /// Gets the champion's path through the tournament.
    /// </summary>
    public async Task<ChampionPathDto?> GetChampionPathAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        
        // Get champion from current tournament in series (if available)
        string? champion = null;
        if (state?.SeriesProgress?.Tournaments != null)
        {
            var currentTournament = state.SeriesProgress.Tournaments
                .FirstOrDefault(t => t.Status == TournamentItemStatus.Completed);
            champion = currentTournament?.Champion;
        }

        if (string.IsNullOrEmpty(champion) || champion == "N/A")
            return null;

        var matches = await _matchFeed.GetMatchesForTeamAsync(champion, 1000);

        var path = new ChampionPathDto
        {
            ChampionName = champion,
            MatchesWon = matches.Count(m => m.WinnerName == champion)
        };

        // Get winning matches in order
        var wins = matches
            .Where(m => m.WinnerName == champion)
            .OrderBy(m => m.CompletedAt)
            .ToList();

        foreach (var match in wins)
        {
            path.Opponents.Add(match.Bot1Name == champion ? match.Bot2Name : match.Bot1Name);
        }

        return path;
    }

    /// <summary>
    /// Gets team progression visualization through tournament stages.
    /// </summary>
    public async Task<ProgressionVisualizationDto> GetProgressionVisualizationAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        var viz = new ProgressionVisualizationDto();

        if (state?.CurrentTournament == null)
            return viz;

        viz.CurrentStage = state.CurrentTournament.Stage.ToString();

        if (state.OverallLeaderboard != null)
        {
            foreach (var team in state.OverallLeaderboard.OrderBy(t => t.Rank))
            {
                viz.Teams.Add(new TeamProgressionDto
                {
                    TeamName = team.TeamName,
                    CurrentRank = team.Rank,
                    Wins = team.TotalWins,
                    Points = team.TotalPoints
                });
            }
        }

        return viz;
    }

    /// <summary>
    /// Gets series visualization showing multiple tournaments.
    /// </summary>
    public async Task<SeriesVisualizationDto> GetSeriesVisualizationAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        var viz = new SeriesVisualizationDto();

        if (state?.SeriesProgress == null)
            return viz;

        viz.TournamentCount = state.SeriesProgress.TotalCount;
        viz.CompletedCount = state.SeriesProgress.CompletedCount;

        foreach (var tournament in state.SeriesProgress.Tournaments)
        {
            viz.Tournaments.Add(new TournamentSeriesItemDto
            {
                TournamentNumber = tournament.TournamentNumber,
                ChampionName = tournament.Champion ?? "TBD",
                Status = tournament.Status.ToString(),
                StartTime = tournament.StartTime,
                EndTime = tournament.EndTime
            });
        }

        return viz;
    }
}

/// <summary>
/// DTO for bracket visualization.
/// </summary>
public class BracketVisualizationDto
{
    public List<BracketRoundDto> Rounds { get; set; } = new();
}

/// <summary>
/// DTO for a bracket round.
/// </summary>
public class BracketRoundDto
{
    public int RoundNumber { get; set; }
    public List<BracketMatchupDto> Matchups { get; set; } = new();
}

/// <summary>
/// DTO for a bracket matchup.
/// </summary>
public class BracketMatchupDto
{
    public string Team1 { get; set; } = string.Empty;
    public string Team2 { get; set; } = string.Empty;
    public string? Winner { get; set; }
}

/// <summary>
/// DTO for tournament tree structure.
/// </summary>
public class TournamentTreeDto
{
    public string CurrentStage { get; set; } = string.Empty;
    public int CurrentRound { get; set; }
    public int TotalRounds { get; set; }
    public List<string> Stages { get; set; } = new();
}

/// <summary>
/// DTO for round-robin grid.
/// </summary>
public class RoundRobinGridDto
{
    public List<string> TeamNames { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> ResultsMatrix { get; set; } = new();
}

/// <summary>
/// DTO for champion path.
/// </summary>
public class ChampionPathDto
{
    public string ChampionName { get; set; } = string.Empty;
    public int MatchesWon { get; set; }
    public List<string> Opponents { get; set; } = new();
}

/// <summary>
/// DTO for team progression.
/// </summary>
public class TeamProgressionDto
{
    public string TeamName { get; set; } = string.Empty;
    public int CurrentRank { get; set; }
    public int Wins { get; set; }
    public int Points { get; set; }
}

/// <summary>
/// DTO for progression visualization.
/// </summary>
public class ProgressionVisualizationDto
{
    public string CurrentStage { get; set; } = string.Empty;
    public List<TeamProgressionDto> Teams { get; set; } = new();
}

/// <summary>
/// DTO for series visualization.
/// </summary>
public class SeriesVisualizationDto
{
    public int TournamentCount { get; set; }
    public int CompletedCount { get; set; }
    public List<TournamentSeriesItemDto> Tournaments { get; set; } = new();
}

/// <summary>
/// DTO for a tournament in a series.
/// </summary>
public class TournamentSeriesItemDto
{
    public int TournamentNumber { get; set; }
    public string ChampionName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
