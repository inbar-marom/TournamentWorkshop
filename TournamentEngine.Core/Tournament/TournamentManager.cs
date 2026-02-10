namespace TournamentEngine.Core.Tournament;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

/// <summary>
/// High-level tournament manager that orchestrates entire tournament execution
/// Uses ITournamentEngine for low-level tournament operations
/// </summary>
public class TournamentManager : ITournamentManager
{
    private readonly ITournamentEngine _engine;
    private readonly IGameRunner _gameRunner;

    public TournamentManager(ITournamentEngine engine, IGameRunner gameRunner)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _gameRunner = gameRunner ?? throw new ArgumentNullException(nameof(gameRunner));
    }

    public async Task<TournamentInfo> RunTournamentAsync(
        List<BotInfo> bots, 
        GameType gameType, 
        TournamentConfig config, 
        CancellationToken cancellationToken = default)
    {
        if (bots == null || bots.Count < 2)
            throw new ArgumentException("At least 2 bots are required for a tournament", nameof(bots));
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        // Initialize tournament
        _engine.InitializeTournament(bots, gameType, config);

        // Main tournament loop - runs until engine signals completion
        while (!_engine.IsTournamentComplete())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Get next batch of matches from the engine
            var matches = _engine.GetNextMatches();
            
            if (matches.Count == 0)
            {
                // No matches available - let engine advance to next phase/round
                _engine.AdvanceToNextRound();
                continue;
            }

            // Execute all matches in the current batch
            // Run sequentially to maintain deterministic order and avoid concurrency issues
            foreach (var (bot1, bot2) in matches)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Execute match via game runner (returns complete MatchResult)
                var matchResult = await _gameRunner.ExecuteMatch(
                    bot1, 
                    bot2, 
                    gameType, 
                    cancellationToken);

                // Record the result - engine updates internal state
                _engine.RecordMatchResult(matchResult);
            }
        }

        return _engine.GetTournamentInfo();
    }
}
