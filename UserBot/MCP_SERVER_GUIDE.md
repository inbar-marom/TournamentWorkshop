# MCP Server Workshop Guide

## Overview

This guide will help you build your own MCP (Model Context Protocol) server that downloads the UserBot template from the TournamentWorkshop repository. Building this server will teach you how MCP enables AI assistants to interact with external tools and APIs.

## What You'll Build

An MCP server with a `download_userbot_template` tool that:
- Fetches the UserBot template files from GitHub
- Creates the proper directory structure locally
- Writes all necessary files to your specified location
- Verifies the download was successful

## UserBot Template Structure

The UserBot template consists of the following files:

```
UserBot/
├── UserBot.sln                              # Visual Studio solution file
├── UserBot.slnx                             # Modern solution file
├── Directory.Build.props                    # MSBuild configuration
├── UserBot.Core/
│   ├── IBot.cs                             # Bot interface (must implement)
│   ├── GameType.cs                         # Game type enumeration
│   ├── GameState.cs                        # Game state class
│   └── UserBot.Core.csproj                 # Core project file
└── UserBot.BasicBot/
    ├── NaiveBot.cs                         # Example bot implementation
    └── UserBot.BasicBot.csproj             # BasicBot project file
```

## Repository Information

- **Repository:** https://github.com/inbar-marom/TournamentWorkshop
- **Branch:** main
- **Template Path:** UserBot/
- **Manifest File:** UserBot/manifest.json

## Download Method: Manifest-Based Approach

You will implement the manifest-based download method. This approach uses a JSON manifest file that lists all required template files.

### Manifest File

**Manifest URL:** `https://raw.githubusercontent.com/inbar-marom/TournamentWorkshop/main/UserBot/manifest.json`

**Manifest Structure:**
```json
{
  "version": "1.0.0",
  "baseUrl": "https://raw.githubusercontent.com/inbar-marom/TournamentWorkshop/main/UserBot",
  "files": [
    "UserBot.sln",
    "UserBot.slnx",
    "Directory.Build.props",
    "UserBot.Core/IBot.cs",
    "UserBot.Core/GameType.cs",
    "UserBot.Core/GameState.cs",
    "UserBot.Core/UserBot.Core.csproj",
    "UserBot.BasicBot/NaiveBot.cs",
    "UserBot.BasicBot/UserBot.BasicBot.csproj"
  ]
}
```

### Implementation Steps

1. **Fetch the manifest.json file** from the GitHub raw URL
2. **Parse the JSON** to extract the file list and baseUrl
3. **For each file in the list:**
   - Construct the full URL: `baseUrl + "/" + filePath`
   - Fetch the file content via HTTP GET request
   - Parse the directory path (e.g., `UserBot.Core/IBot.cs` → `UserBot.Core/`)
   - Create necessary directories if they don't exist
   - Write the file content to disk
4. **Return success** with list of created files

### Key Learning Points

- **HTTP requests** to fetch JSON data and file content
- **JSON parsing** and navigating data structures
- **File system operations** (creating directories, writing files)
- **String manipulation** for building URLs and parsing paths
- **Error handling** for network and file I/O operations
- **Async/await patterns** for non-blocking operations

## MCP Server Requirements

Your MCP server must implement one tool:

### Tool: `download_userbot_template`

**Description:** Downloads the UserBot template from the TournamentWorkshop repository

**Parameters:**
- `targetDirectory` (string, required): Local directory path where UserBot template should be created
  - Example: `C:/MyWorkshop/UserBot` or `/home/user/workshop/UserBot`

**Returns:**
- Success message with:
  - Number of files downloaded
  - Target directory path
  - List of created files
  - Verification that UserBot.sln is buildable

**Error Handling:**
- **Directory already exists:** Either clean it or fail with clear message
- **Network errors:** Retry with exponential backoff (optional)
- **File write errors:** Check permissions and disk space
- **Invalid targetDirectory:** Validate path before starting download
- **Manifest fetch failure:** Clear error message about connectivity
- **Individual file download failure:** Report which file failed and why

**Example Tool Response:**
```json
{
  "success": true,
  "message": "UserBot template downloaded successfully",
  "filesDownloaded": 9,
  "targetDirectory": "C:/MyWorkshop/UserBot",
  "files": [
    "UserBot.sln",
    "UserBot.slnx",
    "Directory.Build.props",
    "UserBot.Core/IBot.cs",
    "UserBot.Core/GameType.cs",
    "UserBot.Core/GameState.cs",
    "UserBot.Core/UserBot.Core.csproj",
    "UserBot.BasicBot/NaiveBot.cs",
    "UserBot.BasicBot/UserBot.BasicBot.csproj"
  ]
}
```

## Building Your MCP Server

### Technology Stack Options

Choose any of these stacks to build your MCP server:

**Node.js/TypeScript (Recommended):**
- Use `@modelcontextprotocol/sdk` npm package
- Good documentation and examples
- Works well with IDE MCP clients via stdio

**Python:**
- Use `mcp` Python package
- Great for quick prototyping
- Good for adding validation logic

**C#:**
- Build custom MCP server using ASP.NET Core
- Native integration with .NET solutions
- Can directly validate .csproj files

### Getting Started with Node.js/TypeScript

1. **Install Dependencies:**
```bash
npm init -y
npm install @modelcontextprotocol/sdk
npm install -D typescript @types/node
```

2. **Initialize TypeScript:**
```bash
npx tsc --init
```

3. **Create MCP Server Structure:**
```
my-mcp-server/
├── package.json
├── tsconfig.json
├── src/
│   └── index.ts          # Main MCP server code
└── dist/                 # Compiled JavaScript output
```

4. **Implement the Tool:**
   - Import MCP SDK
   - Create server instance
   - Register `download_userbot_template` tool
   - Implement manifest-based download logic
   - Handle errors gracefully
   - Return structured results

5. **Build and Test Your Server:**
```bash
npm run build
node dist/index.js
```

### Pseudo-code Algorithm

```
function download_userbot_template(targetDirectory):
    // Step 1: Fetch manifest
    manifestUrl = "https://raw.githubusercontent.com/.../manifest.json"
    manifestData = httpGet(manifestUrl)
    manifest = jsonParse(manifestData)
    
    // Step 2: Validate target directory
    if not isValidPath(targetDirectory):
        throw "Invalid target directory"
    
    // Step 3: Create base directory
    createDirectory(targetDirectory)
    
    // Step 4: Download each file
    downloadedFiles = []
    for each filePath in manifest.files:
        fullUrl = manifest.baseUrl + "/" + filePath
        fileContent = httpGet(fullUrl)
        
        // Extract directory from file path
        dirPath = extractDirectory(filePath)  // e.g., "UserBot.Core/"
        fullDirPath = joinPath(targetDirectory, dirPath)
        
        // Create directory if needed
        if dirPath exists:
            createDirectory(fullDirPath)
        
        // Write file
        fullFilePath = joinPath(targetDirectory, filePath)
        writeFile(fullFilePath, fileContent)
        downloadedFiles.append(filePath)
    
    // Step 5: Return success
    return {
        success: true,
        filesDownloaded: length(downloadedFiles),
        targetDirectory: targetDirectory,
        files: downloadedFiles
    }
```

## Connecting to Your IDE

After building your MCP server, connect it to your IDE with MCP support:

### VS Code with Cline Extension

1. **Install Cline Extension:**
   - Open VS Code
   - Go to Extensions marketplace
   - Search for "Cline"
   - Install the extension

2. **Configure MCP Server:**
   - Open Cline settings
   - Add new MCP server configuration
   - Specify command: `node`
   - Specify args: `["C:/path/to/your/mcp-server/dist/index.js"]`

3. **Configuration Example:**
```json
{
  "mcpServers": {
    "userbot-downloader": {
      "command": "node",
      "args": ["C:/path/to/your/mcp-server/dist/index.js"]
    }
  }
}
```

### JetBrains IDEs (Rider, IntelliJ) with MCP Plugin

1. **Install MCP Plugin:**
   - Open Settings/Preferences
   - Go to Plugins
   - Search for "MCP" or "Model Context Protocol"
   - Install and restart IDE

2. **Configure MCP Server:**
   - Go to Settings → Tools → MCP
   - Add new server configuration
   - Provide server executable path and arguments

3. **Test Connection:**
   - Check IDE status bar for MCP indicator
   - Verify server is running in IDE logs

### Testing Steps

1. **Restart your IDE** after configuration
2. **Verify MCP server connection** (check status indicator or logs)
3. **Use AI assistant in IDE** to request: "Download UserBot template to C:/MyWorkshop/"
4. **Verify all files created** with correct directory structure
5. **Build solution** to confirm validity: `dotnet build UserBot.sln`

## Validation Checklist

After downloading the template, verify:

- [ ] All 9 files exist in correct directory structure
- [ ] `UserBot.sln` can be opened in Visual Studio/Rider
- [ ] Solution builds without errors: `dotnet build UserBot.sln`
- [ ] `IBot.cs` interface contains all 4 game methods:
  - `MakeMove()` for RPSLS
  - `AllocateTroops()` for Colonel Blotto
  - `MakePenaltyDecision()` for Penalty Kicks
  - `MakeSecurityMove()` for Security game
- [ ] `NaiveBot.cs` implements all required methods
- [ ] Line endings are correct (CRLF for Windows, LF for Unix)
- [ ] File encodings are UTF-8
- [ ] No extra or missing files

## Common Issues and Solutions

### Issue: "Cannot find module '@modelcontextprotocol/sdk'"
**Solution:** Run `npm install` in your MCP server directory

### Issue: "Permission denied writing files"
**Solution:** 
- Check target directory permissions
- Run IDE/terminal with appropriate privileges
- Choose a directory you have write access to

### Issue: "GitHub rate limit exceeded"
**Solution:** 
- Raw URLs have 60 requests/hour limit for anonymous users
- For higher limits, add a GitHub personal access token
- Rate limit: 60/hour (anonymous), 5000/hour (authenticated)

### Issue: "Files downloaded but solution won't build"
**Solution:** 
- Check that all .csproj files are valid XML
- Verify Directory.Build.props is present
- Ensure .NET SDK version matches (net10.0 required)
- Check for line ending issues (CRLF vs LF)
- Validate file encodings are UTF-8

### Issue: "IDE doesn't see my MCP server"
**Solution:**
- Verify config file path is correct
- Check JSON syntax in IDE MCP configuration
- Restart IDE completely
- Check MCP server logs for startup errors
- Ensure node/python is in system PATH
- Test server manually: `node dist/index.js`

### Issue: "Manifest fetch fails"
**Solution:**
- Test manifest URL in browser
- Check internet connectivity
- Verify GitHub is accessible
- Check for proxy/firewall issues

### Issue: "Some files fail to download"
**Solution:**
- Check which file failed in error message
- Verify file exists in repository
- Test individual file URL in browser
- Check for special characters in file paths

## Learning Resources

### MCP Protocol
- Official Specification: https://modelcontextprotocol.io/
- SDK Documentation: https://github.com/modelcontextprotocol/sdk
- Example Servers: https://github.com/modelcontextprotocol/servers

### HTTP & File Operations
- Node.js `fs` module: https://nodejs.org/api/fs.html
- Node.js `https` module: https://nodejs.org/api/https.html
- Node.js `path` module: https://nodejs.org/api/path.html
- Axios (HTTP client): https://axios-http.com/

### JSON Processing
- JSON.parse(): https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/JSON/parse
- Working with JSON in Node.js

### Async Programming
- Promises: https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Promise
- Async/Await: https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Statements/async_function

## Next Steps

After successfully downloading the UserBot template:

1. **Understand the Code:**
   - Study `IBot.cs` interface requirements
   - Examine `NaiveBot.cs` implementation
   - Read `GameState.cs` and `GameType.cs`

2. **Learn the Games:**
   - RPSLS (Rock, Paper, Scissors, Lizard, Spock)
   - Colonel Blotto (resource allocation)
   - Penalty Kicks (goalkeeper vs striker)
   - Security (attacker vs defender)

3. **Develop Your Bot:**
   - Create new bot class implementing `IBot`
   - Implement strategies for all 4 games
   - Test locally with mock GameState objects

4. **Prepare for Submission:**
   - (Details will be provided in separate documentation)
   - Clean up code and add comments
   - Verify all methods return valid responses

## Tips for Success

1. **Start Simple:** Get the basic download working first, then add error handling
2. **Test Incrementally:** Test manifest fetch, then single file, then all files
3. **Use Logging:** Add console.log statements to debug your code
4. **Handle Errors:** Network and file operations can fail - handle gracefully
5. **Cross-Platform:** Use `path.join()` instead of string concatenation for paths
6. **Validate Input:** Check targetDirectory parameter before starting work
7. **Clean Code:** Use meaningful variable names and add comments
8. **Ask for Help:** Don't hesitate to ask workshop organizers or peers

## Support

If you encounter issues:
1. Check this guide's troubleshooting section
2. Review MCP server logs for error messages
3. Test GitHub URLs in browser to verify accessibility
4. Ask questions in workshop communication channel
5. Review example MCP servers on GitHub

---

**Workshop Version:** 1.0.0  
**Last Updated:** February 15, 2026  
**Repository:** https://github.com/inbar-marom/TournamentWorkshop

Good luck building your MCP server! 🚀
