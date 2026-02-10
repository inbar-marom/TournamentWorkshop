## Plan: Tournament Engine (C# implementation)

Build the Tournament Engine in C# as a console application with clean components, strong typing, and safe execution. Target .NET 8, MSTest for tests, and clear separation of concerns.

### Steps
1. Create solution/projects: `TournamentEngine.sln`, `TournamentEngine.Core/`, `TournamentEngine.Console/`, `TournamentEngine.Tests/`; set `TargetFramework` to net8.0 via `Directory.Build.props`.
2. Define shared contracts in `TournamentEngine.Core/Common/Contracts.cs` (Bot, MatchResult, TournamentState, enums, custom exceptions).
3. Provide sample bots and tests `TournamentEngine.Tests/` (MSTest: bot loader validations, RPSLS happy path, bracket advancement; dummy bots under `TournamentEngine.Tests/DummyBots/`).
4. Implement Game Runner `TournamentEngine.Core/GameRunner/GameRunner.cs` (RPSLS full; Blotto validation; Penalty/Security placeholders); enforce timeouts via `Task` + cancellation or external process + Job Objects.
5. Implement Tournament Manager `TournamentEngine.Core/Tournament/TournamentManager.cs` (create bracket, byes, record results, advance rounds, champion, bracket summary DTO).
6. Implement Scoring System `TournamentEngine.Core/Scoring/ScoringSystem.cs` (stats, elimination rounds, rankings, summaries).
7. Implement Output/Display `TournamentEngine.Console/Display/ConsoleDisplay.cs` (round headers, match summaries, bracket view, final rankings, stats) with structured logging.
8. Implement CLI entrypoint `TournamentEngine.Console/Program.cs` (load `appsettings.json`, orchestrate bot loading, bracket creation, running rounds, saving results JSON).
9. Implement minimal game modules `TournamentEngine.Core/Games/{Rpsls.cs, Blotto.cs, Penalty.cs, Security.cs}` (interfaces, constants, helpers consumed by Game Runner).
10. Implement Bot Loader `TournamentEngine.Core/BotLoader/BotLoader.cs` (Roslyn scripting or external process isolation; enforce allowed/blocked namespaces, size limits, signature checks, batch team load).
11. Optional API skeleton `TournamentEngine.Api/` (ASP.NET Core minimal API, endpoints, request/response schemas, API key auth, rate limiting middleware); mark optional for POC.
12. Add docs `README.md` and sample configs `TournamentEngine.Console/appsettings.json` (timeouts, resource limits, logging, game settings).

### Further Considerations
1. Bot sandboxing: Option A `Task` + cancellation for soft limits; Option B external process with Windows Job Objects for CPU/memory; Option C container isolation for stricter sandboxing.
2. Performance: baseline sequential; Option A parallelize matches per round via `Task.WhenAll` with degree-of-parallelism; Option B cache bot compilation and minimize logging in hot paths.
3. Penalty/Security rules: implement minimal viable rules or defer with clear TODOs and stable interfaces.
