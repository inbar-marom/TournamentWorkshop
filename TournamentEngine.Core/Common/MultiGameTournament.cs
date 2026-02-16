namespace TournamentEngine.Core.Common;

/// <summary>
/// Represents a multi-game tournament consisting of 4 game types.
/// This is the top-level tournament structure that contains multiple events (game types).
/// </summary>
public class MultiGameTournament
{
    public string TournamentId { get; init; } //
    public Dictionary<GameType, TournamentInfo> Events { get; init; } = new(); //
    public DateTime StartTime { get; init; } //
    public DateTime? EndTime { get; set; } //
    
    /// <summary>
    /// Creates a new multi-game tournament with events for each configured game type.
    /// </summary>
    public MultiGameTournament(string tournamentId, List<BotInfo> bots, TournamentConfig config)
    {
        TournamentId = tournamentId;
        StartTime = DateTime.Now;
        
        // Create an event (TournamentInfo) for each game type
        foreach (var gameType in config.GameTypes)
        {
            // All bots are shared across events (they implement all game types)
            var eventInfo = new TournamentInfo
            {
                TournamentId = tournamentId,
                GameType = gameType,
                State = TournamentState.NotStarted,
                Bots = new List<BotInfo>(bots), // Share the same bot instances
                StartTime = DateTime.Now,
                CurrentRound = 0,
                TotalRounds = 0 // Will be calculated when groups are assigned
            };
            
            Events[gameType] = eventInfo;
        }
    }
    
    /// <summary>
    /// Gets the current event being played (first non-completed event).
    /// </summary>
    public TournamentInfo? GetCurrentEvent()
    {
        // Return first event that is not completed
        // Order: RPSLS → Blotto → Penalty → Security
        var orderedGameTypes = new[]
        {
            GameType.RPSLS,
            GameType.ColonelBlotto,
            GameType.PenaltyKicks,
            GameType.SecurityGame
        };
        
        foreach (var gameType in orderedGameTypes)
        {
            if (Events.TryGetValue(gameType, out var eventInfo) && 
                eventInfo.State !=TournamentState.Completed)
            {
                return eventInfo;
            }
        }
        
        return null; // All events completed
    }
    
    /// <summary>
    /// Checks if all events in the tournament are completed.
    /// </summary>
    public bool IsComplete()
    {
        return Events.Values.All(e => e.State == TournamentState.Completed);
    }
    
    /// <summary>
    /// Gets aggregate scores across all completed events.
    /// </summary>
    public Dictionary<string, int> GetAggregateScores()
    {
        var aggregateScores = new Dictionary<string, int>();
        
        foreach (var eventInfo in Events.Values)
        {
            if (eventInfo.State != TournamentState.Completed)
            {
                continue; // Only count completed events
            }
            
            // Count wins for each bot in this event
            foreach (var matchResult in eventInfo.MatchResults)
            {
                // Initialize bot entries if needed
                if (!aggregateScores.ContainsKey(matchResult.Bot1Name))
                {
                    aggregateScores[matchResult.Bot1Name] = 0;
                }
                if (!aggregateScores.ContainsKey(matchResult.Bot2Name))
                {
                    aggregateScores[matchResult.Bot2Name] = 0;
                }
                
                // Award points based on outcome (1 point per win, 0 for draw/loss)
                if (matchResult.Outcome == MatchOutcome.Player1Wins || 
                    matchResult.WinnerName == matchResult.Bot1Name)
                {
                    aggregateScores[matchResult.Bot1Name]++;
                }
                else if (matchResult.Outcome == MatchOutcome.Player2Wins || 
                         matchResult.WinnerName == matchResult.Bot2Name)
                {
                    aggregateScores[matchResult.Bot2Name]++;
                }
                // Draw: no points awarded to either bot
            }
        }
        
        return aggregateScores;
    }
}
