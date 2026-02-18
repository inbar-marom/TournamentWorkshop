# StrategicMind RPSLS Bot Submission

## Folder Structure

### BotCode/
Contains all C# source code for the StrategicMind bot:
- `StrategicMindBot.cs` - Main bot implementation
- `Core/` - Core utilities (GameRules, HistoryAnalyzer, SecureRandom, PredictionResult)
- `Strategies/` - Five pattern detection strategies
- `MetaLearning/` - Performance tracking and strategy selection
- `UserBot.StrategicMind.csproj` - Project file

### Documentation/
- `plan-workshop.md` - High-level workshop plan
- `plan-rpsls.md` - RPSLS game implementation plan
- `RPSLS-Strategy-Skill.md` - Strategic analysis skill
- `RPSLS-IMPLEMENTATION-SUMMARY.md` - Implementation details
- `RPSLS-USAGE-GUIDE.md` - Usage examples

### Config/
- `plan-workshop.instructions.md` - Coding standards and rules
- `ResearchAgent.agent.md` - Research agent configuration

### Tools/
- `verificationScript.py` - Automated verification (compilation, coding standards, tests, performance)

## Bot Features

**5 Strategic Patterns:**
1. Nash Equilibrium - Unexploitable baseline
2. Frequency Counter - Exploits move bias
3. Markov Chain - Sequential patterns
4. Reactive Counter - Counter-prediction detection
5. N-Gram Pattern - Multi-move sequences

**Meta-Learning:**
- Epsilon-greedy exploration
- Performance tracking
- Weighted ensemble voting

**Test Coverage:** 69 comprehensive tests (100% passing)

**Compliance:** 100% statement ending rule, no double semicolons

## Verification

Run the verification script:
```bash
python3 Tools/verificationScript.py
```

This checks:
1. Compilation success
2. No double semicolons
3. Statement ending rule (// compliance)
4. Test coverage
5. Execution time (<0.3s)
