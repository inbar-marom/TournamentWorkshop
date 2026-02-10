namespace TournamentEngine.Core.Tournament;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using TournamentEngine.Core.Common;

/// <summary>
/// Low-level tournament execution engine for group-stage tournaments
/// Handles tournament state management with phases: initial groups → final group → tiebreaker
/// </summary>
public class GroupStageTournamentEngine : ITournamentEngine
{
    private readonly IGameRunner _gameRunner;
    private readonly IScoringSystem _scoringSystem;
    private TournamentInfo _tournamentInfo;
    private List<Group> _currentGroups;
    private Group? _finalGroup;
    private Queue<(IBot bot1, IBot bot2)> _pendingMatches;
    private Dictionary<string, MatchResult> _matchHistory;
    private Dictionary<string, GroupStanding> _groupStandings;
    private TournamentPhase _currentPhase;
    private int _currentPhaseExpectedMatches;
    private int _currentPhaseRecordedResults;
    private readonly List<string> _eventLog;

    public GroupStageTournamentEngine(IGameRunner gameRunner, IScoringSystem scoringSystem)
    {
        _gameRunner = gameRunner ?? throw new ArgumentNullException(nameof(gameRunner));
        _scoringSystem = scoringSystem ?? throw new ArgumentNullException(nameof(scoringSystem));
        _currentGroups = new List<Group>();
        _pendingMatches = new Queue<(IBot bot1, IBot bot2)>();
        _matchHistory = new Dictionary<string, MatchResult>();
        _groupStandings = new Dictionary<string, GroupStanding>();
        _currentPhase = TournamentPhase.NotStarted;
        _currentPhaseExpectedMatches = 0;
        _currentPhaseRecordedResults = 0;
        _tournamentInfo = null!; // Will be set by InitializeTournament
        _eventLog = new List<string>();
    }

    public TournamentInfo InitializeTournament(List<BotInfo> bots, GameType gameType, TournamentConfig config)
    {
        if (bots == null || bots.Count < 2)
            throw new ArgumentException("At least 2 bots are required for a tournament", nameof(bots));
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        // Create tournament info with unique ID
        var tournamentId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;

        _tournamentInfo = new TournamentInfo
        {
            TournamentId = tournamentId,
            GameType = gameType,
            State = TournamentState.InProgress,
            Bots = bots,
            MatchResults = new List<MatchResult>(),
            Bracket = new Dictionary<int, List<string>>(),
            Champion = null,
            StartTime = startTime,
            EndTime = null,
            CurrentRound = 1,
            TotalRounds = CalculateTotalRounds(bots.Count)
        };

        _currentPhase = TournamentPhase.InitialGroups;
        _matchHistory.Clear();
        _eventLog.Clear();
        Log($"Tournament initialized with {bots.Count} bots for {gameType}");

        var botAdapters = CreateBotAdapters(bots, gameType);
        _currentGroups = CreateInitialGroups(botAdapters);
        _groupStandings = BuildStandingsIndex(_currentGroups);
        var allMatches = GenerateAllGroupMatches(_currentGroups);
        _pendingMatches = new Queue<(IBot bot1, IBot bot2)>(allMatches);
        _currentPhaseExpectedMatches = allMatches.Count;
        _currentPhaseRecordedResults = 0;

        return _tournamentInfo;
    }

    public List<(IBot bot1, IBot bot2)> GetNextMatches()
    {
        if (_currentPhase == TournamentPhase.NotStarted)
            throw new InvalidOperationException("Tournament not initialized");

        return new List<(IBot bot1, IBot bot2)>(_pendingMatches);
    }

    public TournamentInfo RecordMatchResult(MatchResult matchResult)
    {
        if (_currentPhase == TournamentPhase.NotStarted)
            throw new InvalidOperationException("Tournament not initialized");
        if (_currentPhase == TournamentPhase.Completed)
            throw new InvalidOperationException("Tournament is already completed");
        if (matchResult == null)
            throw new ArgumentNullException(nameof(matchResult));

        ValidateBotName(matchResult.Bot1Name, nameof(matchResult));
        ValidateBotName(matchResult.Bot2Name, nameof(matchResult));

        _tournamentInfo.MatchResults.Add(matchResult);

        var historyKey = $"{matchResult.Bot1Name}-vs-{matchResult.Bot2Name}-{_tournamentInfo.MatchResults.Count}";
        _matchHistory[historyKey] = matchResult;

        UpdateStandingsForMatch(matchResult);
        _currentPhaseRecordedResults++;

        // Remove this match from pending queue
        DequeueMatch(matchResult.Bot1Name, matchResult.Bot2Name);

        Log($"Recorded match: {matchResult.Bot1Name} vs {matchResult.Bot2Name} ({matchResult.Outcome})");

        return _tournamentInfo;
    }

    public TournamentInfo AdvanceToNextRound()
    {
        if (_currentPhase == TournamentPhase.NotStarted)
            throw new InvalidOperationException("Tournament not initialized");
        if (_currentPhase == TournamentPhase.Completed)
            throw new InvalidOperationException("Tournament is already completed");

        if (_currentPhaseRecordedResults < _currentPhaseExpectedMatches)
            throw new InvalidOperationException(
                $"Not all matches completed. Expected {_currentPhaseExpectedMatches}, recorded {_currentPhaseRecordedResults}");

        if (_currentPhase == TournamentPhase.InitialGroups)
        {
            // Determine winner from each initial group
            var winners = new List<IBot>();
            foreach (var group in _currentGroups)
            {
                var winner = DetermineGroupWinner(group);
                winners.Add(winner);
                group.IsComplete = true;
            }

            // Create final group from all group winners
            _finalGroup = new Group
            {
                GroupId = "Final-Group",
                Bots = winners,
                Standings = new Dictionary<string, GroupStanding>(),
                IsComplete = false
            };

            foreach (var bot in winners)
            {
                _finalGroup.Standings[bot.TeamName] = new GroupStanding
                {
                    BotName = bot.TeamName,
                    Points = 0,
                    Wins = 0,
                    Losses = 0,
                    Draws = 0,
                    GoalDifferential = 0
                };
            }

            _currentGroups = new List<Group> { _finalGroup };
            _groupStandings = BuildStandingsIndex(_currentGroups);
            var finalMatches = GenerateAllGroupMatches(_currentGroups);
            _pendingMatches = new Queue<(IBot bot1, IBot bot2)>(finalMatches);
            _currentPhaseExpectedMatches = finalMatches.Count;
            _currentPhaseRecordedResults = 0;
            _currentPhase = TournamentPhase.FinalGroup;
            _tournamentInfo.CurrentRound = 2;
            Log($"Advanced to FinalGroup with {winners.Count} finalists");
        }
        else if (_currentPhase == TournamentPhase.FinalGroup)
        {
            var finalGroup = _currentGroups[0];
            var topBots = GetTopContenders(finalGroup);

            if (topBots.Count > 1)
            {
                // Tie detected; move to tiebreaker phase
                var tiebreakerGroup = CreateTiebreakerGroup(topBots);
                _currentGroups = new List<Group> { tiebreakerGroup };
                _groupStandings = BuildStandingsIndex(_currentGroups);

                var tiebreakerMatches = GenerateGroupMatches(tiebreakerGroup);
                _pendingMatches = new Queue<(IBot bot1, IBot bot2)>(tiebreakerMatches);
                _currentPhaseExpectedMatches = tiebreakerMatches.Count;
                _currentPhaseRecordedResults = 0;
                _currentPhase = TournamentPhase.Tiebreaker;
                finalGroup.IsComplete = true;
                Log($"FinalGroup tied; scheduled tiebreaker for {topBots.Count} bots");
            }
            else
            {
                var winner = topBots[0];
                finalGroup.IsComplete = true;

                _tournamentInfo.Champion = winner.TeamName;
                _tournamentInfo.State = TournamentState.Completed;
                _tournamentInfo.EndTime = DateTime.UtcNow;
                _currentPhase = TournamentPhase.Completed;
                _pendingMatches.Clear();
                Log($"Tournament completed. Champion: {winner.TeamName}");
            }
        }
        else if (_currentPhase == TournamentPhase.Tiebreaker)
        {
            var tiebreakerGroup = _currentGroups[0];
            var contenders = GetTopContenders(tiebreakerGroup);
            var winner = contenders.Count == 1 ? contenders[0] : ExecuteTiebreaker(contenders);
            tiebreakerGroup.IsComplete = true;

            _tournamentInfo.Champion = winner.TeamName;
            _tournamentInfo.State = TournamentState.Completed;
            _tournamentInfo.EndTime = DateTime.UtcNow;
            _currentPhase = TournamentPhase.Completed;
            _pendingMatches.Clear();
            Log($"Tournament completed after tiebreaker. Champion: {winner.TeamName}");
        }

        return _tournamentInfo;
    }

    private IBot DetermineGroupWinner(Group group)
    {
        var contenders = GetTopContenders(group);
        return contenders[0];
    }

    private List<IBot> GetTopContenders(Group group)
    {
        var maxPoints = group.Bots.Max(bot => group.Standings[bot.TeamName].Points);
        var tied = group.Bots
            .Where(bot => group.Standings[bot.TeamName].Points == maxPoints)
            .ToList();

        if (tied.Count <= 1)
            return tied;

        var headToHead = GetHeadToHeadPoints(tied);
        var maxHeadToHead = headToHead.Values.Max();
        tied = tied.Where(bot => headToHead[bot.TeamName] == maxHeadToHead).ToList();

        if (tied.Count <= 1)
            return tied;

        var maxGoalDiff = tied.Max(bot => group.Standings[bot.TeamName].GoalDifferential);
        tied = tied.Where(bot => group.Standings[bot.TeamName].GoalDifferential == maxGoalDiff).ToList();

        if (tied.Count <= 1)
            return tied;

        var maxWins = tied.Max(bot => group.Standings[bot.TeamName].Wins);
        tied = tied.Where(bot => group.Standings[bot.TeamName].Wins == maxWins).ToList();

        return tied.OrderBy(bot => bot.TeamName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private Dictionary<string, int> GetHeadToHeadPoints(List<IBot> bots)
    {
        var set = new HashSet<string>(bots.Select(bot => bot.TeamName), StringComparer.OrdinalIgnoreCase);
        var points = bots.ToDictionary(bot => bot.TeamName, _ => 0, StringComparer.OrdinalIgnoreCase);

        foreach (var match in _tournamentInfo.MatchResults)
        {
            if (!set.Contains(match.Bot1Name) || !set.Contains(match.Bot2Name))
                continue;

            points[match.Bot1Name] += GetPointsForOutcome(match.Outcome, true);
            points[match.Bot2Name] += GetPointsForOutcome(match.Outcome, false);
        }

        return points;
    }

    private int GetPointsForOutcome(MatchOutcome outcome, bool isBot1)
    {
        return outcome switch
        {
            MatchOutcome.Player1Wins => isBot1 ? 3 : 0,
            MatchOutcome.Player2Wins => isBot1 ? 0 : 3,
            MatchOutcome.Draw => 1,
            MatchOutcome.BothError => 0,
            MatchOutcome.Player1Error => isBot1 ? 0 : 3,
            MatchOutcome.Player2Error => isBot1 ? 3 : 0,
            _ => 0
        };
    }

    private IBot ExecuteTiebreaker(List<IBot> tiedBots)
    {
        if (tiedBots.Count == 1)
            return tiedBots[0];

        var contenders = new List<IBot>(tiedBots);
        while (contenders.Count > 1)
        {
            var winners = new List<IBot>();
            for (int i = 0; i < contenders.Count; i += 2)
            {
                if (i + 1 >= contenders.Count)
                {
                    winners.Add(contenders[i]);
                    continue;
                }

                var winner = ExecuteSuddenDeath(contenders[i], contenders[i + 1]);
                winners.Add(winner);
            }

            contenders = winners;
        }

        return contenders[0];
    }

    private IBot ExecuteSuddenDeath(IBot bot1, IBot bot2)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var result = _gameRunner
                .ExecuteMatch(bot1, bot2, _tournamentInfo.GameType, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            // Record tiebreaker match result
            _tournamentInfo.MatchResults.Add(result);
            var historyKey = $"{result.Bot1Name}-vs-{result.Bot2Name}-{_tournamentInfo.MatchResults.Count}";
            _matchHistory[historyKey] = result;
            Log($"Recorded tiebreaker match: {result.Bot1Name} vs {result.Bot2Name} ({result.Outcome})");

            if (result.Outcome == MatchOutcome.Player1Wins || result.Outcome == MatchOutcome.Player2Error)
                return bot1;
            if (result.Outcome == MatchOutcome.Player2Wins || result.Outcome == MatchOutcome.Player1Error)
                return bot2;
        }

        return Random.Shared.Next(2) == 0 ? bot1 : bot2;
    }

    private GroupStageSummary BuildPhaseSummary(TournamentPhase phase, List<Group> groups)
    {
        var summaries = new List<GroupSummary>();
        var winners = new List<string>();

        foreach (var group in groups)
        {
            var standings = GetGroupStandings(group);
            var matches = GetGroupMatchSummaries(group);
            var winner = group.IsComplete ? DetermineGroupWinner(group).TeamName : string.Empty;

            if (!string.IsNullOrWhiteSpace(winner))
                winners.Add(winner);

            summaries.Add(new GroupSummary
            {
                GroupId = group.GroupId,
                Standings = standings,
                Matches = matches,
                Winner = winner
            });
        }

        return new GroupStageSummary
        {
            PhaseId = phase.ToString(),
            Groups = summaries,
            PhaseWinners = winners
        };
    }

    private List<GroupStanding> GetGroupStandings(Group group)
    {
        return group.Standings.Values
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalDifferential)
            .ThenByDescending(s => s.Wins)
            .ThenBy(s => s.BotName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<(string bot1, string bot2, string result)> GetGroupMatchSummaries(Group group)
    {
        var set = new HashSet<string>(group.Bots.Select(bot => bot.TeamName), StringComparer.OrdinalIgnoreCase);
        var summaries = new List<(string bot1, string bot2, string result)>();

        foreach (var match in _tournamentInfo.MatchResults)
        {
            if (!set.Contains(match.Bot1Name) || !set.Contains(match.Bot2Name))
                continue;

            summaries.Add((match.Bot1Name, match.Bot2Name, match.Outcome.ToString()));
        }

        return summaries;
    }

    private Group CreateTiebreakerGroup(List<IBot> bots)
    {
        var group = new Group
        {
            GroupId = "Tiebreaker",
            Bots = bots,
            Standings = new Dictionary<string, GroupStanding>(),
            IsComplete = false
        };

        foreach (var bot in bots)
        {
            group.Standings[bot.TeamName] = new GroupStanding
            {
                BotName = bot.TeamName,
                Points = 0,
                Wins = 0,
                Losses = 0,
                Draws = 0,
                GoalDifferential = 0
            };
        }

        return group;
    }

    public bool IsTournamentComplete()
    {
        return _tournamentInfo?.State == TournamentState.Completed;
    }

    public TournamentInfo GetTournamentInfo()
    {
        return _tournamentInfo;
    }

    public int GetCurrentRound()
    {
        return _tournamentInfo?.CurrentRound ?? 0;
    }

    public GroupStageSummary GetCurrentPhaseSummary()
    {
        if (_currentPhase == TournamentPhase.NotStarted)
            throw new InvalidOperationException("Tournament not initialized");

        return BuildPhaseSummary(_currentPhase, _currentGroups);
    }

    public string ExportPhaseSummaryJson(bool indented = true)
    {
        var options = new JsonSerializerOptions { WriteIndented = indented };
        return JsonSerializer.Serialize(GetCurrentPhaseSummary(), options);
    }

    public string ExportMatchResultsJson(bool indented = true)
    {
        var options = new JsonSerializerOptions { WriteIndented = indented };
        return JsonSerializer.Serialize(_tournamentInfo.MatchResults, options);
    }

    public IReadOnlyList<string> GetEventLog()
    {
        return _eventLog.AsReadOnly();
    }

    public List<IBot> GetRemainingBots()
    {
        if (_currentPhase == TournamentPhase.NotStarted)
            throw new InvalidOperationException("Tournament not initialized");

        var remaining = new List<IBot>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in _currentGroups)
        {
            foreach (var bot in group.Bots)
            {
                if (seen.Add(bot.TeamName))
                    remaining.Add(bot);
            }
        }

        if (remaining.Count == 0 && !string.IsNullOrWhiteSpace(_tournamentInfo?.Champion))
        {
            remaining.Add(ResolveBotByName(_tournamentInfo.Champion));
        }

        return remaining;
    }

    public List<(IBot bot, int placement)> GetFinalRankings()
    {
        if (_currentPhase == TournamentPhase.NotStarted)
            throw new InvalidOperationException("Tournament not initialized");
        if (_tournamentInfo.State != TournamentState.Completed)
            throw new InvalidOperationException("Tournament is not complete");

        var rankings = _scoringSystem.GenerateFinalRankings(_tournamentInfo);
        var ordered = rankings.OrderBy(r => r.FinalPlacement).ToList();

        var results = new List<(IBot bot, int placement)>(ordered.Count);
        foreach (var ranking in ordered)
        {
            var bot = ResolveBotByName(ranking.BotName);
            results.Add((bot, ranking.FinalPlacement));
        }

        return results;
    }

    // Helper methods

    private int CalculateTotalRounds(int botCount)
    {
        // For group stage: Phase 1 (initial groups) + Phase 2 (final group) + potential tiebreaker
        // This is a simplified calculation - actual rounds depend on group progression
        return 3;
    }

    private IBot ResolveBotByName(string botName)
    {
        foreach (var group in _currentGroups)
        {
            var match = group.Bots.FirstOrDefault(bot => bot.TeamName == botName);
            if (match != null)
                return match;
        }

        var botInfo = _tournamentInfo.Bots.FirstOrDefault(bot => bot.TeamName == botName);
        if (botInfo != null)
            return new BotInfoAdapter(botInfo.TeamName, _tournamentInfo.GameType);

        return new BotInfoAdapter(botName, _tournamentInfo.GameType);
    }

    internal List<Group> CreateInitialGroups(List<IBot> bots)
    {
        if (bots == null || bots.Count == 0)
            throw new ArgumentException("Bots list cannot be null or empty", nameof(bots));

        var groupCount = Math.Max(1, bots.Count / 10);
        var shuffledBots = new List<IBot>(bots);
        ShuffleInPlace(shuffledBots);

        var groups = new List<Group>(groupCount);
        for (int i = 0; i < groupCount; i++)
        {
            groups.Add(new Group
            {
                GroupId = $"Group-{i + 1}",
                Bots = new List<IBot>(),
                Standings = new Dictionary<string, GroupStanding>(),
                IsComplete = false
            });
        }

        for (int i = 0; i < shuffledBots.Count; i++)
        {
            var groupIndex = i % groupCount;
            var group = groups[groupIndex];
            var bot = shuffledBots[i];
            group.Bots.Add(bot);
            group.Standings[bot.TeamName] = new GroupStanding
            {
                BotName = bot.TeamName,
                Points = 0,
                Wins = 0,
                Losses = 0,
                Draws = 0,
                GoalDifferential = 0
            };
        }

        return groups;
    }

    internal List<(IBot bot1, IBot bot2)> GenerateGroupMatches(Group group)
    {
        if (group == null)
            throw new ArgumentNullException(nameof(group));
        if (group.Bots == null)
            throw new ArgumentException("Group bots cannot be null", nameof(group));

        var matches = new List<(IBot bot1, IBot bot2)>();
        for (int i = 0; i < group.Bots.Count; i++)
        {
            for (int j = i + 1; j < group.Bots.Count; j++)
            {
                matches.Add((group.Bots[i], group.Bots[j]));
            }
        }

        return matches;
    }

    private List<(IBot bot1, IBot bot2)> GenerateAllGroupMatches(List<Group> groups)
    {
        var matches = new List<(IBot bot1, IBot bot2)>();
        foreach (var group in groups)
        {
            matches.AddRange(GenerateGroupMatches(group));
        }

        return matches;
    }

    private Dictionary<string, GroupStanding> BuildStandingsIndex(List<Group> groups)
    {
        var standings = new Dictionary<string, GroupStanding>();
        foreach (var group in groups)
        {
            foreach (var entry in group.Standings)
            {
                standings[entry.Key] = entry.Value;
            }
        }

        return standings;
    }

    private void ShuffleInPlace(List<IBot> bots)
    {
        for (int i = bots.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (bots[i], bots[j]) = (bots[j], bots[i]);
        }
    }

    private void DequeueMatch(string bot1Name, string bot2Name)
    {
        var remaining = new Queue<(IBot bot1, IBot bot2)>();
        bool removed = false;
        while (_pendingMatches.Count > 0)
        {
            var match = _pendingMatches.Dequeue();
            if (!removed &&
                ((match.bot1.TeamName == bot1Name && match.bot2.TeamName == bot2Name) ||
                 (match.bot1.TeamName == bot2Name && match.bot2.TeamName == bot1Name)))
            {
                removed = true;
                continue;
            }
            remaining.Enqueue(match);
        }
        _pendingMatches = remaining;
    }

    private void ValidateBotName(string botName, string paramName)
    {
        if (string.IsNullOrWhiteSpace(botName))
            throw new ArgumentException("Bot name cannot be empty", paramName);
        if (!_groupStandings.ContainsKey(botName))
            throw new ArgumentException($"Bot '{botName}' not found in tournament", paramName);
    }

    private void UpdateStandingsForMatch(MatchResult matchResult)
    {
        if (!_groupStandings.TryGetValue(matchResult.Bot1Name, out var bot1Standing) ||
            !_groupStandings.TryGetValue(matchResult.Bot2Name, out var bot2Standing))
        {
            throw new ArgumentException("Match result contains unknown bot names", nameof(matchResult));
        }

        var scoreDiff = matchResult.Bot1Score - matchResult.Bot2Score;
        bot1Standing.GoalDifferential += scoreDiff;
        bot2Standing.GoalDifferential -= scoreDiff;

        switch (matchResult.Outcome)
        {
            case MatchOutcome.Player1Wins:
                bot1Standing.Points += 3;
                bot1Standing.Wins++;
                bot2Standing.Losses++;
                break;
            case MatchOutcome.Player2Wins:
                bot2Standing.Points += 3;
                bot2Standing.Wins++;
                bot1Standing.Losses++;
                break;
            case MatchOutcome.Draw:
                bot1Standing.Points += 1;
                bot2Standing.Points += 1;
                bot1Standing.Draws++;
                bot2Standing.Draws++;
                break;
            case MatchOutcome.BothError:
                // No points awarded
                break;
            case MatchOutcome.Player1Error:
                bot2Standing.Points += 3;
                bot2Standing.Wins++;
                bot1Standing.Losses++;
                break;
            case MatchOutcome.Player2Error:
                bot1Standing.Points += 3;
                bot1Standing.Wins++;
                bot2Standing.Losses++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(matchResult.Outcome), matchResult.Outcome, "Unknown match outcome");
        }
    }

    private List<IBot> CreateBotAdapters(List<BotInfo> bots, GameType gameType)
    {
        var adapters = new List<IBot>(bots.Count);
        foreach (var bot in bots)
        {
            adapters.Add(new BotInfoAdapter(bot.TeamName, gameType));
        }

        return adapters;
    }

    private void Log(string message)
    {
        _eventLog.Add($"{DateTime.UtcNow:O} {message}");
    }
}

/// <summary>
/// Adapter that exposes BotInfo as an IBot for grouping and scheduling
/// </summary>
internal sealed class BotInfoAdapter : IBot
{
    public string TeamName { get; }
    public GameType GameType { get; }

    public BotInfoAdapter(string teamName, GameType gameType)
    {
        TeamName = teamName;
        GameType = gameType;
    }

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Bot execution is not available for adapters");
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Bot execution is not available for adapters");
    }

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Bot execution is not available for adapters");
    }

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Bot execution is not available for adapters");
    }
}

/// <summary>
/// Represents a group of bots in the tournament
/// </summary>
public class Group
{
    public required string GroupId { get; init; }
    public required List<IBot> Bots { get; init; }
    public Dictionary<string, GroupStanding> Standings { get; init; } = new();
    public bool IsComplete { get; set; }
}

/// <summary>
/// Tracks a bot's standing within a group
/// </summary>
public class GroupStanding
{
    public required string BotName { get; init; }
    public int Points { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int GoalDifferential { get; set; }
}

/// <summary>
/// Tournament phase tracker
/// </summary>
public enum TournamentPhase
{
    NotStarted,
    InitialGroups,
    FinalGroup,
    Tiebreaker,
    Completed
}
