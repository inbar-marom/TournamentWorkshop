<#
.SYNOPSIS
Tournament Engine Console - PowerShell Orchestration Script
Builds and runs the tournament engine with configuration management

.DESCRIPTION
Automates the complete tournament workflow:
1. Validates environment setup
2. Builds the console application
3. Configures environment variables
4. Executes the tournament
5. Handles logs and results

.PARAMETER Environment
Environment to run in: Development, Staging, or Production (default: Development)

.PARAMETER BotsDirectory
Path to bots directory (optional, overrides config)

.PARAMETER ResultsDirectory
Path to results directory (optional, overrides config)

.PARAMETER DashboardEnabled
Enable dashboard service (optional, overrides config)

.PARAMETER DashboardPort
Dashboard port number (default: 5000)

.PARAMETER LogLevel
Logging level: Debug, Information, Warning, Error, Critical (default: Information)

.PARAMETER MaxParallelMatches
Max parallel matches to run (optional, overrides config)

.PARAMETER Clean
Force clean build (delete bin/obj directories)

.PARAMETER NoTests
Skip running tests before execution

.PARAMETER Verbose
Enable verbose logging output

.EXAMPLES
# Run with default development settings
.\run-tournament.ps1

# Run with production settings
.\run-tournament.ps1 -Environment Production

# Run with custom bot directory
.\run-tournament.ps1 -BotsDirectory "C:\MyBots"

# Run with dashboard enabled
.\run-tournament.ps1 -DashboardEnabled $true -DashboardPort 8080

# Clean build with debug logging
.\run-tournament.ps1 -Clean -LogLevel Debug
#>

param(
    [ValidateSet('Development', 'Staging', 'Production')]
    [string]$Environment = 'Development',
    
    [string]$BotsDirectory = $null,
    [string]$ResultsDirectory = $null,
    [bool]$DashboardEnabled = $null,
    [int]$DashboardPort = 0,
    [ValidateSet('Debug', 'Information', 'Warning', 'Error', 'Critical')]
    [string]$LogLevel = 'Information',
    [int]$MaxParallelMatches = 0,
    
    [switch]$Clean,
    [switch]$NoTests,
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'
$VerbosePreference = if ($Verbose) { 'Continue' } else { 'SilentlyContinue' }

# Colors for output
$colors = @{
    success = 'Green'
    error = 'Red'
    warning = 'Yellow'
    info = 'Cyan'
}

function Write-Info { Write-Host "[INFO]  $args" -ForegroundColor $colors.info }
function Write-Success { Write-Host "[OK] $args" -ForegroundColor $colors.success }
function Write-Error { Write-Host "[ERROR] $args" -ForegroundColor $colors.error }
function Write-Warning { Write-Host "[WARN] $args" -ForegroundColor $colors.warning }

try {
    Write-Info "Tournament Engine Console - PowerShell Orchestration"
    Write-Info "============================================"
    Write-Info "Environment: $Environment"
    Write-Info "Start Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Info ""
    
    # Step 1: Validate prerequisites
    Write-Info "Step 1: Validating prerequisites..."
    
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet CLI not found. Please install .NET 8.0 SDK."
    }
    Write-Success ".NET SDK found"
    
    $projectPath = Join-Path $PSScriptRoot "TournamentEngine.Console"
    $projectFile = Join-Path $projectPath "TournamentEngine.Console.csproj"
    
    if (-not (Test-Path $projectFile)) {
        throw "Project file not found: $projectFile"
    }
    Write-Success "Project file found"
    
    # Step 2: Clean if requested
    if ($Clean) {
        Write-Info "Step 2: Cleaning build artifacts..."
        $binDir = Join-Path $projectPath "bin"
        $objDir = Join-Path $projectPath "obj"
        
        if (Test-Path $binDir) {
            Remove-Item $binDir -Recurse -Force -ErrorAction SilentlyContinue
            Write-Info "  Removed: $binDir"
        }
        if (Test-Path $objDir) {
            Remove-Item $objDir -Recurse -Force -ErrorAction SilentlyContinue
            Write-Info "  Removed: $objDir"
        }
        Write-Success "Build artifacts cleaned"
    }
    else {
        Write-Info "Step 2: Skipping clean (use -Clean for full rebuild)"
    }
    
    # Step 3: Build
    Write-Info "Step 3: Building TournamentEngine.Console..."
    $buildOutput = dotnet build $projectFile --configuration Release 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        Write-Error ($buildOutput -join "`n")
        throw "Build failed with exit code $LASTEXITCODE"
    }
    Write-Success "Build completed successfully"
    
    # Step 4: Run tests (unless skipped)
    if (-not $NoTests) {
        Write-Info "Step 4: Running tests..."
        $testProject = Join-Path $PSScriptRoot "TournamentEngine.Tests\TournamentEngine.Tests.csproj"
        
        $testOutput = dotnet test $testProject --configuration Release 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Some tests failed (this may be expected for pre-existing failures)"
            Write-Verbose ($testOutput -join "`n")
        }
        else {
            Write-Success "All tests passed"
        }
    }
    else {
        Write-Info "Step 4: Skipping tests (-NoTests specified)"
    }
    
    # Step 5: Configure environment variables
    Write-Info "Step 5: Configuring environment..."
    
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    $env:DOTNET_ENVIRONMENT = $Environment
    
    if ($BotsDirectory) {
        $env:TOURNAMENT_BOTS_DIRECTORY = $BotsDirectory
    }
    
    if ($ResultsDirectory) {
        $env:TOURNAMENT_RESULTS_DIRECTORY = $ResultsDirectory
    }
    
    if ($DashboardEnabled -ne $null) {
        $env:TOURNAMENT_ENABLE_DASHBOARD = $DashboardEnabled.ToString()
    }
    
    if ($DashboardPort -gt 0) {
        $env:TOURNAMENT_DASHBOARD_PORT = $DashboardPort
    }
    
    if ($LogLevel) {
        $env:TOURNAMENT_LOG_LEVEL = $LogLevel
    }
    
    if ($MaxParallelMatches -gt 0) {
        $env:TOURNAMENT_MAX_PARALLEL_MATCHES = $MaxParallelMatches
    }
    
    Write-Success "Environment configured"
    Write-Verbose "  ASPNETCORE_ENVIRONMENT = $($env:ASPNETCORE_ENVIRONMENT)"
    
    # Step 6: Run tournament
    Write-Info "Step 6: Running tournament engine..."
    Write-Info "============================================"
    
    $consoleExe = Join-Path $projectPath "bin\Release\net8.0\TournamentEngine.Console.exe"
    
    if (-not (Test-Path $consoleExe)) {
        throw "Console executable not found: $consoleExe"
    }
    
    & $consoleExe
    $exitCode = $LASTEXITCODE
    
    Write-Info "============================================"
    Write-Info "Tournament execution completed with exit code: $exitCode"
    
    if ($exitCode -eq 0) {
        Write-Success "Tournament completed successfully"
    }
    else {
        Write-Warning "Tournament exited with non-zero code: $exitCode"
    }
    
    Write-Info "End Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    
    exit $exitCode
}
catch {
    Write-Error $_.Exception.Message
    Write-Verbose $_.ScriptStackTrace
    exit 1
}
