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
            
            // Get scores for this event (to be implemented based on scoring system)
            // For now, placeholder logic
            foreach (var bot in eventInfo.Bots)
            {
                if (!aggregateScores.ContainsKey(bot.TeamName))
                {
                    aggregateScores[bot.TeamName] = 0;
                }
                // Score calculation will be implemented in Sub-Phase 3.4
            }
        }
        
        return aggregateScores;
    }
}
