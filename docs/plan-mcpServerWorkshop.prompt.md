# Plan: Participant-Built MCP Server for UserBot Template Download
## Overview
Workshop participants will build their own MCP (Model Context Protocol) server that downloads the UserBot template from the TournamentWorkshop GitHub repository. This approach teaches them MCP protocol basics while providing practical experience with file operations, API integration, and AI assistant tooling.
## Goals
- Enable participants to build a working MCP server from scratch
- Teach manifest-based file downloading from GitHub
- Provide comprehensive documentation without giving away the implementation
- Allow participants to connect their MCP server to their IDE (via MCP client support)
- Validate their understanding through successful template download and solution compilation
## Prerequisites
- Basic programming knowledge (Node.js/TypeScript, Python, or C#)
- Git and GitHub understanding
- Development environment with Node.js or Python installed
- IDE with MCP client support (e.g., VS Code with Cline extension, JetBrains IDEs with MCP plugin)
## Implementation Steps
### Step 1: Create Documentation Files
Create two new files in the UserBot directory:
**File 1: `MCP_SERVER_GUIDE.md`**
- Location: `UserBot/MCP_SERVER_GUIDE.md`
- Content: Comprehensive guide covering:
  - UserBot template structure (9 files across 2 projects)
  - Manifest-based download implementation method
  - MCP server requirements and tool specification
  - Technology stack options (Node.js/TypeScript recommended)
  - IDE integration instructions (VS Code with Cline, JetBrains with MCP plugin)
  - Validation checklist for successful download
  - Troubleshooting common issues
  - Learning resources and next steps
**File 2: `manifest.json`**
- Location: `UserBot/manifest.json`
- Content: JSON file listing all template files with metadata
- Fields: version, description, baseUrl, repository, files array, requiredForSubmission, buildCommand, targetFramework
- Purpose: Enable manifest-based download implementation
### Step 2: Define MCP Server Requirements
Participants must implement one MCP tool:
**Tool Name:** `download_userbot_template`
**Parameters:**
- `targetDirectory` (string, required): Local path where template will be created
**Behavior:**
- Downloads all 9 UserBot template files from GitHub
- Creates proper directory structure (`UserBot.Core/` and `UserBot.BasicBot/` folders)
- Writes files to disk with correct content and line endings
- Returns success message with file count, paths, and verification status
**Error Handling:**
- Directory conflicts (already exists)
- Network failures with retry logic
- File system permission errors
- Invalid target paths
### Step 3: Manifest-Based Download Method

Participants will implement the manifest-based download approach:

**Implementation Steps:**
- Fetch `manifest.json` from raw GitHub URL
- Parse JSON to get the file list
- Loop through files and download each using baseUrl + file path
- Create necessary directory structure (`UserBot.Core/` and `UserBot.BasicBot/` folders)
- Write each file to disk with proper encoding

**Manifest URL:** `https://raw.githubusercontent.com/inbar-marom/TournamentWorkshop/main/UserBot/manifest.json`

**Key Learning Points:**
- HTTP requests to fetch JSON data
- JSON parsing and data structure navigation
- File system operations (creating directories, writing files)
- String concatenation for building URLs
- Error handling for network and file I/O operations

**Advantages of This Approach:**
- Easy to maintain (add files by updating manifest)
- Clear file list for participants to verify
- Good for teaching basic HTTP and file operations
- Simple to debug and test incrementally
### Step 4: Guide Participants Through MCP Server Development

**Technology Stack Options:**
- **Node.js/TypeScript** (Recommended): Use `@modelcontextprotocol/sdk`, good documentation, stdio support
- **Python**: Use `mcp` package, quick prototyping, good for validation logic
- **C#**: ASP.NET Core, native .NET integration, can validate .csproj files

**Development Steps:**
1. Initialize project with dependencies
2. Set up TypeScript configuration (if using Node.js)
3. Create MCP server structure
4. Implement `download_userbot_template` tool
5. Implement the manifest-based download logic
6. Add error handling and logging
7. Test locally by running the server
### Step 5: Configure IDE Integration

**IDE Options with MCP Support:**

**VS Code with Cline Extension:**
- Install Cline extension from VS Code marketplace
- Configure MCP server in Cline settings
- Add server configuration with command and args pointing to your MCP server executable

**JetBrains IDEs (Rider, IntelliJ, etc.) with MCP Plugin:**
- Install MCP plugin from JetBrains marketplace
- Configure MCP server in IDE settings
- Add server configuration with appropriate command and arguments

**Configuration Format Example (varies by IDE/extension):**
```json
{
  "mcpServers": {
    "userbot-downloader": {
      "command": "node",
      "args": ["path/to/mcp-server/dist/index.js"]
    }
  }
}
```

**Testing Steps:**
1. Restart your IDE after configuration
2. Verify MCP server connection in IDE (check status indicator or logs)
3. Use AI assistant in IDE to request: "Download UserBot template to [your-directory]"
4. Verify all files created with correct structure
5. Build solution to confirm validity: `dotnet build UserBot.sln`
### Step 6: Define Validation Criteria
Participants verify successful implementation by checking:
- All 9 files exist in correct directory structure
- `UserBot.sln` opens in Visual Studio/Rider
- Solution builds without errors: `dotnet build UserBot.sln`
- `IBot.cs` contains all 4 game methods (MakeMove, AllocateTroops, MakePenaltyDecision, MakeSecurityMove)
- `NaiveBot.cs` implements all required methods
- Line endings are appropriate for platform (CRLF for Windows, LF for Unix)
## Repository Structure
After implementation, the repository will have:
``
TournamentWorkshop/
├── UserBot/
│   ├── MCP_SERVER_GUIDE.md          # New: Participant guide
│   ├── manifest.json                 # New: File listing for Method 1
│   ├── UserBot.sln
│   ├── UserBot.slnx
│   ├── Directory.Build.props
│   ├── UserBot.Core/
│   │   ├── IBot.cs
│   │   ├── GameType.cs
│   │   ├── GameState.cs
│   │   └── UserBot.Core.csproj
│   └── UserBot.BasicBot/
│       ├── NaiveBot.cs
│       └── UserBot.BasicBot.csproj
└── docs/
    └── (future: API submission docs)
``
## Learning Outcomes
Participants will learn:
- How MCP protocol enables AI-tool integration
- File operations and directory management
- HTTP requests and JSON data fetching
- JSON parsing and data structures
- Error handling and validation patterns
- Configuration management and environment setup
- Testing and debugging distributed systems
## Troubleshooting Guide
Common issues and solutions:
- **Module not found**: Run `npm install` in MCP server directory
- **Permission denied**: Check target directory permissions
- **GitHub rate limit**: Use raw URLs (60/hour anonymous) or add token for higher limits (5000/hour)
- **Build failures**: Verify .csproj files, check .NET SDK version, verify line endings
- **IDE MCP connection**: Check IDE/extension config, verify JSON syntax, restart IDE completely, check MCP server logs
## Next Steps
After successful download:
1. Study `IBot.cs` interface requirements
2. Examine `NaiveBot.cs` example implementation
3. Read `GameState.cs` and `GameType.cs` to understand game mechanics
4. Develop custom bot strategy for all 4 games
5. Prepare for submission (future workshop phase)
## Further Considerations

1. **Workshop Time Management**: Building an MCP server from scratch may take 30-60 minutes. Consider providing time estimates so participants can plan accordingly based on available workshop time and learning goals.

2. **Alternative Difficulty Levels**: Consider offering "beginner" (manifest method with detailed pseudocode and hints) and "advanced" (minimal guidance, participants discover the solution) tracks to accommodate different skill levels.

3. **Testing Infrastructure**: Participants may benefit from a simple test suite or validation script that verifies their MCP server works correctly before connecting to their IDE. This could check for proper tool registration, parameter handling, and file creation.

4. **Rate Limiting and Caching**: If many participants download simultaneously, GitHub rate limits may cause issues. Consider documenting how to implement local caching of the manifest or files to reduce repeated API calls during development/testing.

5. **Cross-Platform Considerations**: File paths, line endings, and directory separators differ between Windows/macOS/Linux. The guide should emphasize platform-agnostic path handling (e.g., `path.join()` in Node.js) and proper line ending management.

6. **Security Best Practices**: Since participants will write files to disk, emphasize path validation to prevent directory traversal attacks and sanitize user input for `targetDirectory` parameter.


