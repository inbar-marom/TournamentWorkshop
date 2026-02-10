# Tournament Engine

A C# implementation of a tournament system for AI bot competitions, supporting multiple games including Rock Paper Scissors Lizard Spock, Colonel Blotto, Penalty Kicks, and Security Game.

## Project Structure

```
TournamentEngine.sln
├── TournamentEngine.Core/          # Core library with business logic
│   ├── Common/                     # Shared contracts and models
│   └── GlobalUsings.cs            # Global using statements
├── TournamentEngine.Console/       # Console application entry point
└── TournamentEngine.Tests/        # Unit tests (MSTest)
```

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- Git

### Building the Project

```bash
dotnet build TournamentEngine.sln
```

### Running the Console Application

```bash
dotnet run --project TournamentEngine.Console
```

### Running Tests

```bash
dotnet test TournamentEngine.Tests
```

## Implementation Status

### ✅ Completed Steps

1. **Project Structure Setup**
   - Solution file with proper project references
   - Three projects: Core library, Console app, Test project
   - .NET 8.0 target framework via Directory.Build.props

2. **Shared Contracts**
   - Complete domain models and interfaces
   - Game types: RPSLS, Colonel Blotto, Penalty Kicks, Security Game
   - Bot interface with async methods and cancellation token support
   - Exception handling classes
   - Tournament state management classes

### 🚧 Planned Implementation

3. Sample bots and tests
4. Game Runner implementation
5. Tournament Manager
6. Scoring System  
7. Console Display
8. CLI Entry Point
9. Game Modules
10. Bot Loader with sandboxing
11. Optional REST API
12. Documentation and configuration

## Architecture

The tournament engine follows a clean architecture pattern with:

- **Core Domain Models**: Game state, match results, bot information
- **Interfaces**: IBot for all bot implementations
- **Configuration**: Tournament settings and timeouts
- **Error Handling**: Custom exceptions for different failure scenarios

## Game Support

- **RPSLS**: Rock Paper Scissors Lizard Spock (50 rounds)
- **Colonel Blotto**: Resource allocation across 5 battlefields (100 troops total)
- **Penalty Kicks**: Goalkeeper vs striker decisions
- **Security Game**: Attacker vs defender scenarios

## License

This project is part of an AI Productivity Workshop demonstration.
