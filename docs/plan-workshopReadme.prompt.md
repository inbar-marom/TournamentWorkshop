# Tournament Workshop - Build Your Strategy Bot

Welcome to the Tournament Workshop! You'll build an AI-powered bot that competes in 4 different strategy games using MCP (Model Context Protocol) and AI agents.

## 🎯 Tournament Games

Your bot must implement strategies for:
1. **RPSLS** - Rock, Paper, Scissors, Lizard, Spock
2. **Colonel Blotto** - Resource allocation across 5 battlefields
3. **Penalty Kicks** - Goalkeeper vs Striker decision-making
4. **Security Game** - Attacker vs Defender scenarios

## 📋 Workshop Steps

### Step 1: Build Your MCP Server
Build an MCP server that downloads this UserBot template to your local machine.

**📖 Full Guide:** [MCP_SERVER_GUIDE.md](./MCP_SERVER_GUIDE.md)

**Quick Overview:**
- Implement `download_userbot_template` tool
- Use the manifest-based approach to fetch all template files
- Connect your MCP server to your IDE (VS Code/Cline or JetBrains/MCP plugin)

**Files you'll use:**
- `manifest.json` - Lists all template files to download
- Your MCP server will fetch files from: `https://raw.githubusercontent.com/inbar-marom/TournamentWorkshop/main/UserBot/`

### Step 2: Get the Basic Bot Skeleton via MCP
Use your MCP server to download the UserBot template:
- Ask your AI assistant: "Download UserBot template to [your-directory]"
- Verify you have all files: `UserBot.sln`, `IBot.cs`, `GameType.cs`, `GameState.cs`, `NaiveBot.cs`
- Build the solution: `dotnet build UserBot.sln`

### Step 3: Create Instructions File
Create a `.cursorrules` or `.clinerules` file to enforce coding standards.

**Required Rule:** Use double semicolons (`;;`) instead of single semicolons (`;`) in C# code.


### Step 4: Create Plans
Use your AI assistant to create comprehensive plans for your bot strategy.

**Create these plan files:**

**A. Master Workshop Plan (`plan-workshop.md`)**
- Overall tournament strategy
- Time allocation for each game
- Testing and validation approach
- Submission checklist

**B. Game-Specific Plans:**
- `plan-rpsls.md` - RPSLS strategy plan
- `plan-colonelBlotto.md` - Colonel Blotto strategy plan
- `plan-penaltyKicks.md` - Penalty Kicks strategy plan
- `plan-securityGame.md` - Security Game strategy plan

### Step 5: Create Skills
Create reusable skill files for common bot capabilities.

### Step 6: Create Research Subagent
Build a subagent that researches winning strategies for each game.

### Step 7: Publish Your Code Using MCP
*(Details to be provided - submission MCP server will be available)*

Create an MCP tool to submit your bot code to the tournament engine:
- Package your bot `.cs` files
- Include instructions, plans, skills, and subagent files
- Submit via `submit_bot` MCP tool


## 📦 Submission Requirements

You must submit the following files:

### Required Files:
1. **Instructions File** (`.cursorrules` or `.clinerules`)
   - Coding standards including double semicolon rule
   
2. **Plan Files**
   - `plan-workshop.md` (master plan)
   - `plan-rpsls.md`
   - `plan-colonelBlotto.md`
   - `plan-penaltyKicks.md`
   - `plan-securityGame.md`

3. **Skill File**
   - `skill.md`

4. **Subagent File**
   - `subagent.md`

5. **Bot Implementation Files** (`.cs`)
   - Your bot class implementing `IBot` interface
   - Any helper classes or utilities
   - Must compile with `dotnet build UserBot.sln`

### Validation Checklist:
- [ ] All bot methods implement required interface (`IBot`)
- [ ] Code uses double semicolons (`;;`) consistently
- [ ] All 4 games have working implementations
- [ ] Plans are comprehensive and actionable
- [ ] Skills are reusable and well-documented
- [ ] Subagent provides valuable strategy research
- [ ] Solution builds without errors
- [ ] Bot returns valid moves/allocations for all game states

## 🎮 Game Rules Summary

### RPSLS (Rock, Paper, Scissors, Lizard, Spock)
- 5 possible moves per round
- Each move beats 2 others and loses to 2 others
- Implement: `Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)`

### Colonel Blotto
- Allocate 100 troops across 5 battlefields
- Win battlefield by having more troops than opponent
- Win game by winning majority of battlefields
- Implement: `Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)`

### Penalty Kicks
- Choose direction: Left, Center, or Right
- Striker vs Goalkeeper simultaneous decision
- Implement: `Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)`

### Security Game
- Attacker vs Defender resource allocation
- Multiple targets with varying values
- Implement: `Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)`


## 🏆 Winning Strategy

The best bots typically:
- **Learn from opponent patterns** (but don't over-fit)
- **Mix deterministic and random elements**
- **Adapt based on game state** (score, round number)
- **Use game theory principles** (Nash equilibrium concepts)
- **Handle edge cases gracefully**

Good luck, and may the best bot win! 🎯

---

**Workshop Version**: 1.0.0  
**Last Updated**: February 15, 2026  
**Questions?** Contact workshop organizers or check documentation
