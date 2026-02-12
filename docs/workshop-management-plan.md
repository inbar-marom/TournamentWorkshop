# AI Productivity Workshop Orchestration Plan

A 2-hour, hands-on workshop to build and submit C# bots into the Tournament Engine, run tournaments, and visualize results. Participants use .NET 8/9 on Windows with PowerShell, clone the repo, restore/build, and implement bots following the existing API. Facilitators prepare prebuilt tournament flows, validate environment, and streamline submissions via a folder-based workflow aligned with the solution.

## Agenda (2 hours)
- 0:00–0:05 — Welcome & objectives; repo overview (README, docs).
- 0:05–0:15 — Architecture walkthrough (Core components, GameRunner, BotLoader, TournamentManager, Scoring).
- 0:15–0:25 — Environment check: clone, restore, build; run smoke tests.
- 0:25–0:40 — Bot API intro & submission workflow.
- 0:40–1:10 — Build your bot (participants implement); facilitators desk-check.
- 1:10–1:25 — Collect submissions and run tournament (console entrypoint or script).
- 1:25–1:40 — Results & dashboard; analyze scoring.
- 1:40–1:50 — Optimizations & parallel execution discussion.
- 1:50–2:00 — Wrap-up: share artifacts, feedback, next steps.

## Prerequisites & Materials
- SDKs: .NET 8 (preferred), .NET 9 acceptable; Git for Windows.
- IDE: JetBrains Rider/Visual Studio 2022/VS Code; PowerShell v5.1 or newer.
- Repo: GitHub remote; local path under `C:\Users\{user}\RiderProjects\workshop`.
- Documents: Engine spec and implementation plans under `docs/`.
- Equipment: Projector, reliable Wi‑Fi, power strips, whiteboard.
- Offline bundles: Zip of the repo (no .git), prebuilt artifacts.

## Facilitators’ Prep Checklist
- Verify build on a clean Windows machine; confirm `global.json` matches an installed SDK.
- Dry-run: clone, restore, build, run tests, run a sample tournament.
- Prepare a starter bot template; validate BotLoader and GameRunner with it.
- Confirm dashboard and simulator start up; smoke test hub connections.
- Prepare quickstart cards with common commands and paths.
- Prepare contingency USB sticks (repo zip + results viewer instructions).

## Environment Setup (Windows/PowerShell)
- Clone: `git clone https://github.com/inbar-marom/TournamentWorkshop.git`
- Open solution: `TournamentEngine.sln` in your IDE.
- Restore & build: `dotnet restore`; `dotnet build`.
- Run unit tests: `dotnet test TournamentEngine.sln`.
- Smoke run: start the console app; verify outputs under `TournamentEngine.Console/results/`.

## Submission Workflow
- Folder-based submission (recommended):
  - Each team has a folder under a designated `bots/Teams/{TeamName}`.
  - Bots implement the required interfaces used by GameRunner/BotLoader.
  - Facilitator adds team entries to a configuration file used by the console orchestrator.
- Alternative: Pull request to a fork that adds the bot folder.
- Validation: Facilitator runs the bot against a reference bot to confirm compliance.

## Tournament Rules
- Format: Single-elimination for large groups; round-robin for ≤12 teams.
- Determinism: Seed RNG where applicable; fixed rounds per match.
- Timeouts & limits enforced by GameRunner; invalid outputs lead to disqualification.
- Scoring: Track wins/losses; rankings derived from bracket results.

## Sandboxing & Constraints
- Time budget per move (game-specific); enforce via GameRunner.
- Restricted APIs for bots; no network/file system access beyond allowed paths.
- Exceptions or timeouts cause match loss; engine continues without crashing.

## Support & Triage
- Roles: Build triage lead, dashboard ops lead, roaming facilitators.
- Channels: On-site Q&A plus chat (Teams/Slack) with FAQ pinned.
- Escalation: Provide offline bundles; switch to CLI if IDE issues arise.

## Contingency Plans
- Network outage: Use offline zip and run from local artifacts.
- NuGet issues: Pre-warmed local cache; alternate feeds.
- Time overrun: Reduce rounds; cap teams; run only one game.
- Dashboard failure: Use console output and saved results JSON.

## Wrap-up
- Share leaderboard; save results to a shared directory.
- Collect feedback via quick survey.
- Publish winning bot and lessons learned; point to next steps in docs.

## Responsibilities
- Organizers: Prepare environment, orchestrate tournament, manage submissions, support.
- Participants: Install SDK/IDE, implement bots per API, validate locally, submit on time.

## Risk Mitigations
- Pin SDK via `global.json` to an installed version; disable workload auto-imports if needed.
- Provide starter kit and reference bot; keep submission workflow simple and folder-based.
- Clear logging and error guidance; quick validation script for facilitators.

## Mandatory AI Tool Checkpoints (Required)
To ensure participants practice AI-assisted development responsibly and reproducibly, the following checkpoints are mandatory. Facilitators will verify evidence before allowing tournament submissions.

- Planning phase — Strategy document:
  - Requirement: Each team must produce a brief strategy document in either BMAP or Spec-It format.
  - Location: Commit the file under `docs/teams/{TeamName}/strategy.md`.
  - Content: High-level approach, chosen game(s), bot design outline, risks/assumptions, and test plan.
  - Verification: Facilitator checks the file exists, is non-empty, and follows the template.

- Implementation — Copilot CLI review output:
  - Requirement: Run `copilot review` (or equivalent AI code review tool) on your bot project and capture the output.
  - Location: Commit the output as `docs/teams/{TeamName}/copilot-review.txt`.
  - Content: Include the full review output and a short note of actions taken.
  - Verification: Facilitator verifies the presence of the file and a brief summary of addressed findings.

- Optional bonus — Sub-agent architecture:
  - Suggestion: Use a separate agent for opponent modeling or strategy simulation.
  - Location: Document in `docs/teams/{TeamName}/subagent-plan.md` and reference in `strategy.md`.
  - Recognition: Bonus points in wrap-up; may be highlighted in results.

### Enforcement Mechanisms
- Submission gating:
  - The tournament orchestrator checks that both `strategy.md` and `copilot-review.txt` exist under `docs/teams/{TeamName}/` before accepting bot entries.
  - Missing artifacts cause the submission to be rejected with a clear message.

- Pre-run validation script:
  - A simple preflight validator scans `docs/teams/*/` and confirms required evidence is present per team.
  - Teams with missing artifacts are flagged; facilitators help them complete checkpoints.

- Timeboxed checkpoints in agenda:
  - 0:25–0:40 — Strategy drafting (BMAP/Spec-It) with facilitator spot checks.
  - 1:00–1:10 — Run `copilot review` and commit the output; quick triage of the findings.

- Templates and quickstart:
  - Provide `docs/templates/strategy-bmap.md` and `docs/templates/strategy-specit.md`.
  - Provide `docs/templates/copilot-review-readme.md` with minimal instructions to run and save output.

### Quick Commands (PowerShell)
- Create team docs folder and add strategy:
```powershell
New-Item -ItemType Directory docs\teams\TeamName | Out-Null
Copy-Item docs\templates\strategy-bmap.md docs\teams\TeamName\strategy.md
```
- Run AI review and save output:
```powershell
copilot review | Tee-Object -FilePath docs\teams\TeamName\copilot-review.txt
git add docs\teams\TeamName\strategy.md docs\teams\TeamName\copilot-review.txt
git commit -m "docs(team): add strategy and copilot review evidence"
```
