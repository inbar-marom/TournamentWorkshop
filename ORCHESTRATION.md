# Tournament Engine Console - Orchestration Scripts

This directory includes automated orchestration scripts for building and running the tournament engine on different platforms.

## Prerequisites

- **.NET 8.0 SDK** - Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
- **PowerShell 5.0+** (Windows) or **Bash 4.0+** (Linux/Mac)
- **Bot source files** in the configured bots directory

## Scripts Overview

### PowerShell Script: `run-tournament.ps1`

Automated orchestration for Windows environments.

**Usage:**
```powershell
.\run-tournament.ps1 [options]
```

**Options:**
```
-Environment <env>           Environment: Development, Staging, Production (default: Development)
-BotsDirectory <path>        Path to bots directory (optional, overrides config)
-ResultsDirectory <path>     Path to results directory (optional, overrides config)
-DashboardEnabled <bool>     Enable dashboard service (optional, overrides config)
-DashboardPort <int>         Dashboard port number (default: 5000)
-LogLevel <level>            Logging level: Debug, Information, Warning, Error, Critical
-MaxParallelMatches <int>    Max parallel matches to run
-Clean                       Force clean build (delete bin/obj)
-NoTests                     Skip running tests
-Verbose                     Enable verbose output
```

**Examples:**
```powershell
# Run with default development settings
.\run-tournament.ps1

# Run in production with custom bot directory
.\run-tournament.ps1 -Environment Production -BotsDirectory "C:\Tournament\Bots"

# Enable dashboard on custom port with debug logging
.\run-tournament.ps1 -DashboardEnabled $true -DashboardPort 8080 -LogLevel Debug

# Clean rebuild with warning-level logging
.\run-tournament.ps1 -Clean -LogLevel Warning
```

### Bash Script: `run-tournament.sh`

Automated orchestration for Linux/Mac environments.

**Setup:**
```bash
chmod +x run-tournament.sh
```

**Usage:**
```bash
./run-tournament.sh [options]
```

**Options:**
```
-e, --environment ENV           Environment: Development, Staging, Production (default: Development)
-b, --bots-dir PATH            Path to bots directory (optional, overrides config)
-r, --results-dir PATH         Path to results directory (optional, overrides config)
-d, --dashboard BOOL           Enable dashboard service (optional, overrides config)
-p, --dashboard-port PORT      Dashboard port number (default: 5000)
-l, --log-level LEVEL          Logging level: Debug, Information, Warning, Error, Critical
-m, --max-parallel NUM         Max parallel matches to run
-c, --clean                    Force clean build (delete bin/obj)
-t, --no-tests                 Skip running tests
-v, --verbose                  Enable verbose output
-h, --help                     Show help message
```

**Examples:**
```bash
# Run with default development settings
./run-tournament.sh

# Run in production with custom directories
./run-tournament.sh -e Production -b /home/user/bots -r /home/user/results

# Enable dashboard with verbose output
./run-tournament.sh -d true -p 8080 -v

# Clean build with debug logging
./run-tournament.sh -c -l Debug
```

## Script Workflow

Both scripts follow the same execution workflow:

1. **Validate Prerequisites**
   - Check .NET SDK installation
   - Verify project files exist

2. **Clean (Optional)**
   - Remove `bin/` and `obj/` directories if `--clean` is specified
   - Useful for forcing a complete rebuild

3. **Build**
   - Compile TournamentEngine.Console in Release configuration
   - Generates optimized executable/DLL

4. **Test (Optional)**
   - Run all unit tests with `-NotTests`/`--no-tests` to skip
   - Validates code quality before execution

5. **Configure Environment**
   - Set environment variables from command-line parameters
   - Override `appsettings.json` configuration

6. **Execute Tournament**
   - Run the compiled tournament engine
   - Stream output directly to console
   - Display execution results

## Environment Configuration

### Via Script Parameters

Parameters override configuration file values:

```powershell
# PowerShell example
.\run-tournament.ps1 -BotsDirectory "C:\Bots" -LogLevel Debug -MaxParallelMatches 8
```

```bash
# Bash example
./run-tournament.sh -b /var/bots -l Debug -m 8
```

### Via Environment Variables

Set environment variables before running (useful for CI/CD):

```powershell
# PowerShell
$env:TOURNAMENT_BOTS_DIRECTORY = "C:\Bots"
$env:TOURNAMENT_LOG_LEVEL = "Debug"
$env:ASPNETCORE_ENVIRONMENT = "Production"
```

```bash
# Bash
export TOURNAMENT_BOTS_DIRECTORY=/var/bots
export TOURNAMENT_LOG_LEVEL=Debug
export ASPNETCORE_ENVIRONMENT=Production
```

### Via Configuration Files

Edit `appsettings.json` or environment-specific files:
- `appsettings.Development.json`
- `appsettings.Production.json`

## Common Workflows

### Development Testing

```powershell
# PowerShell
.\run-tournament.ps1 -Environment Development -LogLevel Debug -NoTests
```

```bash
# Bash
./run-tournament.sh -e Development -l Debug -t
```

### Production Execution

```powershell
# PowerShell
.\run-tournament.ps1 -Environment Production -LogLevel Information
```

```bash
# Bash
./run-tournament.sh -e Production -l Information
```

### Dashboard Integration

```powershell
# PowerShell - Dashboard on port 8080
.\run-tournament.ps1 -DashboardEnabled $true -DashboardPort 8080
```

```bash
# Bash - Dashboard on port 8080
./run-tournament.sh -d true -p 8080
```

### CI/CD Pipeline

```powershell
# PowerShell - Skip tests, clean build, produce logs
.\run-tournament.ps1 -Environment Production -Clean -LogLevel Information
```

```bash
# Bash - Skip tests, clean build with verbose output
./run-tournament.sh -e Production -c -v
```

## Troubleshooting

**Build fails: "Project file not found"**
- Ensure scripts are in the workshop root directory
- Verify `TournamentEngine.Console` folder exists

**Build fails: ".NET SDK not found"**
- Install .NET 8.0 SDK from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
- Verify `dotnet` is in PATH: `dotnet --version`

**Tests fail**
- Some pre-existing test failures may occur (expected)
- Use `--no-tests` or `-NoTests` to skip testing
- Check test output for actual failures

**Dashboard won't start**
- Verify dashboard application is installed
- Check port number is not in use
- Use `-DashboardPort` / `-p` to specify alternative port

**Bot loading fails**
- Verify bots directory exists at configured path
- Ensure bot C# files are valid and compile
- Check logs for specific validation errors

## Environment Variables Reference

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Development | .NET environment (Development/Staging/Production) |
| `TOURNAMENT_BOTS_DIRECTORY` | ./bots | Directory containing bot source files |
| `TOURNAMENT_RESULTS_DIRECTORY` | ./results | Directory for tournament results export |
| `TOURNAMENT_ENABLE_DASHBOARD` | false | Enable dashboard service (true/false) |
| `TOURNAMENT_DASHBOARD_PORT` | 5000 | Dashboard service port |
| `TOURNAMENT_DASHBOARD_URL` | http://localhost:5000 | Dashboard service URL |
| `TOURNAMENT_LOG_LEVEL` | Information | Logging level (Debug/Information/Warning/Error/Critical) |
| `TOURNAMENT_MOVE_TIMEOUT` | 5 | Bot move timeout in seconds |
| `TOURNAMENT_MAX_PARALLEL_MATCHES` | 5 | Maximum parallel match executions |

## Exit Codes

- **0**: Tournament completed successfully
- **1**: Script error (build failed, prerequisites missing, etc.)
- **Other**: Tournament execution returned non-zero code

## Performance Optimization

### For Maximum Performance
```bash
# Linux/Mac
./run-tournament.sh -e Production -l Warning -m 16 -t

# Windows
.\run-tournament.ps1 -Environment Production -LogLevel Warning -MaxParallelMatches 16 -NoTests
```

### For Development/Debugging
```bash
# Linux/Mac
./run-tournament.sh -e Development -l Debug -v

# Windows
.\run-tournament.ps1 -Environment Development -LogLevel Debug -Verbose
```

## Notes

- Scripts use Release build configuration for better performance
- Verbose mode shows additional execution details
- Environment variables are only set for the script execution (not persisted)
- Clean builds are recommended after major code changes
- Test failures may occur but should be expected (pre-existing GameRunner issues)

## Support

For issues or improvements, refer to the main project documentation or commit logs.
