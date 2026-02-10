# AI Productivity Workshop: Bot Tournament

## Overview

This workshop is the second part of an AI training day, following lectures on:

- AI Agents and Sub-Agents Overview
- Copilot Agent Instructions
- Agent Skills
- AI Planning Models (Spec-It, Open Spec, BMAP)
- MCP Basics
- GitHub Copilot CLI
- AI Across the Dev Lifecycle
- Everyday AI at Work

---

## Workshop Concept

### Core Idea

Participants will build their own bot "player" that competes against other bots in a tournament. The goal is to apply as many concepts from the lectures as possible—through hands-on practice, not just following written instructions.

### Format

- **Tournament structure:** Group-stage format with two phases per game
- **Match format:** 1v1 matches in round-robin within groups
- **Scoring:** Points awarded per match; group winners advance
- **Tie-breaker:** Single-elimination or sudden-death if tied after group stages

### Game Selection Criteria

- Moderate rule complexity (not too simple, not overwhelming)
- Requires basic strategy that can outperform opponents
- Suitable for non-programmers who work in the software industry

### Tools Available to All Participants

- VS Code
- GitHub Copilot (Chat + CLI)

---

## Workshop Requirements

| Category | Requirement |
|----------|-------------|
| **Duration** | ~150 minutes |
| **Participants** | 80–120 teams (bots) |
| **Skill level** | ~50–60% developers, rest are non-developers in tech |
| **AI tool usage** | Mandatory (Copilot CLI, planning docs) |
| **Submission** | Automated system accepts bot code |
| **Judging** | Automated tournament engine runs all matches |

---

## Proposed Games (4 Rounds)

| Round | Game | Complexity | Strategy Depth |
|-------|------|------------|----------------|
| 1 | Rock-Paper-Scissors-Lizard-Spock (with hand reading) | Low | Pattern recognition |
| 2 | Colonel Blotto | Medium | Resource allocation, game theory |
| 3 | Security Game (Defender vs. Attacker) | Medium | Prediction, incomplete information |
| 4 | Penalty Kicks | Low-Medium | Opponent modeling, mixed strategies |

---

## Workshop Stages

### Stage 1: Introduction (10 min)

- Explain tournament rules and scoring
- Demo: Build a simple bot live using Copilot
- Distribute bot template and submission instructions

### Stage 2: Development Phase (80 min)

| Time | Activity | Purpose |
|------|----------|---------|
| 0–20 min | Planning | Write strategy doc using BMAP/Spec-It (mandatory) |
| 20–50 min | Implementation | Code bot with Copilot assistance |
| 50–60 min | Intermediate submission (unscored) | Test bot against sample opponents; see results without points |
| 60–80 min | Refinement | Improve strategy based on test results |

### Stage 3: Final Submission (10 min)

- Lock submissions
- Validate all bots run without errors
- Display submission confirmation to each team

### Stage 4: Tournament Execution (15 min)

| Time | Activity |
|------|----------|
| 3 min | Round 1: RPSLS tournament (group stage 1 → group stage 2 → tiebreaker if needed) |
| 3 min | Round 2: Colonel Blotto tournament (group stage 1 → group stage 2 → tiebreaker if needed) |
| 3 min | Round 3: Security Game tournament (group stage 1 → group stage 2 → tiebreaker if needed) |
| 3 min | Round 4: Penalty Kicks tournament (group stage 1 → group stage 2 → tiebreaker if needed) |

### Stage 5: Results & Debrief (10 min)

- Announce final standings
- Highlight winning strategies
- Q&A and discussion

---

## Mandatory AI Tool Checkpoints

| Checkpoint | Required Evidence |
|------------|-------------------|
| Planning phase | Strategy document (BMAP or Spec-It format) |
| Implementation | Copilot CLI review output (`copilot review`) |
| Optional bonus | Sub-agent architecture (separate agent for opponent modeling) |

---

## Submission Milestones

| Milestone | Time | Scored? | Purpose |
|-----------|------|---------|---------|
| Test submission | ~60 min | ❌ No | Validate bot runs; see sample match results |
| Final submission | ~90 min | ✅ Yes | Official entry for tournament |

---

## Open Decisions (Pending Approval)

- Avoid games that involve option to just random the decision and get 50% chances of win

---

## Ready for Approval

Submit for review after confirming:

- Game selection (4 games above)
- Tournament structure (two-phase group stage → tiebreaker if needed)
- Mandatory AI tool usage per stage

After approval, proceed to:

- Game specification documents (input/output format per game)
- Bot template implementation
- Tournament engine development
