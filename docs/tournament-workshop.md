Welcome to the **Tournament Workshop!**  
In this workshop, you'll build an AI‑powered bot that competes in **four different strategy games** using **GitHub Copilot AI agents**.

---

# 🏆 Goal

Build a competitive bot that earns points by winning matches across all tournament games.

Your objective is to **maximize total points** across all events.

At the end of the workshop, you will submit your bot to run in a tournament to determine which bot has the better strategy.

Important: Do not write, update, or fix code manually during this workshop. All coding, building, running, and testing must be performed via GitHub Copilot (agents/CLI).

‌

---

# 🎮 Tournament Games

Your bot must implement strategic behavior for:

1. **RPSLS** — Rock, Paper, Scissors, Lizard, Spock
2. **Colonel Blotto** — Allocate resources across battlefields
3. **Penalty Kicks** — Striker vs Goalkeeper simultaneous decisions   
  \[bonus - not mandatory for submission\]
4. **Security Game** — Attacker/Defender resource allocation   
  \[bonus - not mandatory for submission\]

---

# 📋 Workshop Steps

---

## **Prerequisites**

* Install **VS Code (/ VisualStudio / Rider)**
* Install **GitHub Copilot extension** and sign in
* Enable **GitHub Copilot Agents / MCP**
* Connect to **Confluence MCP** (site URL + API token) (https://hpe-mcp-registry-api.portal.eastus.azure-apicenter.ms/: MCP Installation → API Definition)

---

## **Step 0 — Set Up GitHub Copilot + Confluence MCP**

Use GitHub Copilot in VS Code with the Confluence MCP to **clone this page into your workspace**.

https://hpe-mcp-registry-api.portal.eastus.azure-apicenter.ms/

Tasks:

* Create a local folder (e.g., `/docs` on your working workspace)
* Ask Copilot to save this page as  
  `tournament-workshop.md`

Note: This step is not mandatory. If you run into issues, you can use the provided tournament-workshop.md and place it under a /docs folder in your workspace. However, it's strongly recommended not to skip this step and to ensure the Atlassian/Confluence MCP works. Use the MCP registry link above for troubleshooting.  

---

## **Step 1 — Build Your MCP Server**

Create an MCP server that can download the UserBot template.  
Template defines the **APIs** your bot must support.

Guide:  
https://zerto.atlassian.net/wiki/spaces/ZE/pages/2064384690

Tasks:

* Install Python (Ask GitHub Copilot to install it and to add it to the path)
* Create the server
* Connect VS Code to it
* Configure it to implement all documented tools (APIs) in

    * https://zerto.atlassian.net/wiki/spaces/~382399296/pages/2071429159
    

---

## **Step 2 — Download the Bot Skeleton via MCP**

Ask Copilot:

> **"Download UserBot template "**
>
> where the resource name is "UserBot"

Verify the files:

* `UserBot.sln`
* `IBot.cs`
* `GameType.cs`
* `GameState.cs`
* `NaiveBot.cs`

Build it (ask copilot to do so)

---

## **Step 3 — Create Instructions File**

Create a GitHub Copilot Instructions file:

📄 **Filename:** `copilot-instructions.md`

**Location:** `.github/copilot-instructions.md`

Guide:  
https://zerto.atlassian.net/wiki/spaces/ZE/pages/2073034857

Must enforce:

* Every function must have Unit Tests
* Every statement must end with a **Two forward slashes(**`//`**)**

    * e.g. `int a = 5;//`
    
* Only classes and functions may contain documentation
* No inline comments in the rest of the code

---

## **Step 4 — High-Level Bot Plan**

Using Copilot and your `tournament-workshop.md`, ask copilot to generate a comprehensive plan as a new file. (prompt e.g.: "use `tournament-workshop.md` to to generate a comprehensive plan as `plan-workshop.md`"

📄 **Filename:** `plan-workshop.md`

Review the created Plan and modify it to your preferences

Guide:  
https://zerto.atlassian.net/wiki/spaces/ZE/pages/2072150168

Plan must include:

* Tournament overview
* Architecture & implementation steps
* Strategy integration approach
* Testing & validation
* Submission checklist
* A **unique bot name**

---

## **Step 5 — Build the Research Agent**

Using Copilot, your `tournament-workshop.md` and `plan-workshop.md`, ask Copilot to implement the creation of a GitHub Copilot subagent dedicated to **strategy research** for a generic game.

📄 **Filename:** `Research.agent.md`

Review the created Agent and modify it to your preferences

Guide:  
https://zerto.atlassian.net/wiki/spaces/ZE/pages/2072215756

Content:

* How the agent performs research
* How it informs strategy selection

---

### 🔁 Recommended Workflow Order

It is strongly recommended to work on **one game at a time** across steps **6–10 + verify**.  
Complete Steps 6 → 7 → 8 → 9 → 10 → **verify** for a single game, then return to Step 6 and repeat for the next game.  
This ensures each game has:

* its research complete
* its skill file finished
* its strategy plan defined
* its implementation verified

Only after finishing one game cycle should you move on to the next.

---

## **Step 6 — Create Game Skills**

Create one skill file per game using the VS Code Command Palette (File: New File… or Command Palette: "> Create New File"). Do this BEFORE using Copilot Chat, to ensure the correct path, filename, and starter structure. Then ask the new Research Agent (switch to it in your IDE) and the `plan-workshop.md` only to fill in the placeholders while preserving the file's pre‑existing structure. Do not let Copilot create, rename, or move the file — only fill content.

Guide:  
https://zerto.atlassian.net/wiki/spaces/ZE/pages/2076278786

Each skill file must include:

* Game rules
* Strategy options
* Pros / cons of each
* Notes relevant to bot implementation

📄 **Filenames:**

* `RPSLS_Skill.md`
* `colonelBlotto_Skill.md`
* `penaltyKicks_Skill.md`
* `securityGame_Skill.md`

Review the created Skiles and modify it to your preferences

---

## **Step 7 — Create Game-Specific Plans**

For each game, use the copilot (regular agent), the dedicated game Skill and the `plan-workshop.md`, define a development plan describing the logic your bot will implement.

Instruct the copilot on which strategy to choose and provide guidelines for behaving in the game. Strongly advise the copilot to ask you questions about your options and guidelines.

📄 **Filenames:**

* `plan-rpsls.md`
* `plan-colonelBlotto.md`
* `plan-penaltyKicks.md`
* `plan-securityGame.md`

Review the created Plans and modify them to your preferences

---

## **Step 8 — Implement the Bot**

Instruct Copilot to execute the specific game plans by implementing the actual strategies in your bot. Apply the relevant skill for each game during implementation.  
All outputs must follow the interface defined in `IBot.cs`.

During this phase, instruct the copilot to create tests that verify the logic and the strategy to make sure it work as you intended.

---

## **Step 9 — Verification Script**

\[Bonus - not mandatory for submission\]

Use GitHub Copilot and the `plan-workshop.md` to create a script for GitHub Copilot CLI to verify the output

Use GitHub Copilot CLI to run the script to validate your entire submission.

📄 **Filename:** `verificationScript.py`

Guide:

https://github.com/features/copilot/cli

Checks must include:

* Compilation
* Two forward slashes(//) rule
* 50 percent of required files UT coverage
* Execution time per API call (<0.3 sec)

Note: This step is not mandatory. If you run into issues, you can skip this verification. 

---

## **Step 10 — Package Your Submission**

Use Copilot CLI to create a **folder** containing:

* All bot `.cs` files
* Instructions file
* High-Level Bot Plan:
* Research Agent
* Skills
* Game plans
* Verification script \[Bonus\]

---

## **Optional Step — Verify via MCP**

Use the above (from step 1) MCP submission tool named `verify` that uploads your folder submission to the tournament engine.  
https://zerto.atlassian.net/wiki/spaces/~382399296/pages/2071429159

### Recommended

Run the verification few times during the workshop to make sure all align 

---

## **Step 11 — Submit via MCP**

When finished and after verifying the results, use copilot and the `plan-workshop.md` to submit your bot for the competition.

Use the above (from step 1) MCP submission tool named `submit` that uploads your submission to the tournament engine.  
https://zerto.atlassian.net/wiki/spaces/~382399296/pages/2071429159

---

# 📦 Submission Requirements

Your code must:

* Implement the **IBot** interface in full
* Include helper classes if needed.
* Use only approved .NET libraries 

    * ```csharp
      "System",
      "System.Collections.Generic",
      "System.Linq",
      "System.Text",
      "System.Numerics",
      "System.Threading",
      "System.Threading.Tasks",
      "System.IO",
      "System.Text.RegularExpressions",
      "System.Diagnostics",
      "TournamentEngine.Core.Common"
      ```
    
* Target the required .NET version `net8.0`
* Build with:

```
dotnet build UserBot.sln
```

* Ensure all methods respond in **< 0.3 sec**
* If your bot crashes or exceeds the per-move response time limit (< 0.3 sec), it will be automatically disqualified and removed from the tournament.
* Size limits enforced:

    * Max per file: 50KB
    * Max total: 500KB
    
* Max memory/space per bot: 450 MB (including system libraries)
* Follow coding rules:

    * UT coverage for every function
    * Two forward slashes(//) in code
    * Documentation **only** on classes and methods
    

---

# 🎮 Game Rules Summary

### General

* 4 events per tournament
* Bots grouped into 10 groups
* Round‑robin within each group
* Winners proceed to final stage
* Each win gives 1 point
* Ties resolved by tiebreaker

### Game Formats

* **RPSLS:** 10 rounds per opponent  
  https://bigbangtheory.fandom.com/wiki/Rock,_Paper,_Scissors,_Lizard,_Spock
* **Colonel Blotto:** 5 rounds per opponent  
  https://en.wikipedia.org/wiki/Blotto_game
* **Penalty Kicks:** 10 rounds per opponent  
  Left, Center, or Right   
  In Penalty Kicks, each match has nine rounds where one bot is always the shooter and the other is always the goalkeeper. The shooter scores 1 point by choosing a different direction (Left, Center, or Right) than the goalkeeper, while the goalkeeper earns 2 points for a save by matching the shooter's direction. After all nine rounds, the bot with the higher total score wins the game.
* **Security Game:** 5 rounds per opponent  
  In the Security Game, both bots play five rounds. One bot is the attacker for all five rounds, and the other bot is the defender for all five rounds. There are several targets, each with a fixed value. At the start of each round, the defender distributes defense units across the targets, and the attacker chooses one target to attack.

    Scoring works like this:


    * If the defender puts zero defense on the attacked target, the attacker gets the full value of that target, and the defender gets zero.
    * If the defender puts some defense on the target but less than its value, the attacker gets the amount that was not defended, and the defender gets the amount that was defended.
    * If the defender puts defense equal to or more than the value of the target, the attacker gets zero and the defender gets the full value.
    

    After all five rounds, both sides add up their points. The player with the higher total wins the game.



---

# ✔ Improved Ending

Good luck — and remember, the goal of this exercise is **to level up your AI‑assisted coding skills** and build a high‑performance tournament bot! 🎯