#!/usr/bin/env pwsh
# Submit a bot folder to the Tournament Engine API
# Usage: .\submit-bot-folder.ps1 -BotFolder "path/to/bot" -TeamName "MyTeam" [-ApiUrl "http://localhost:5000"]

param(
    [Parameter(Mandatory=$true)]
    [string]$BotFolder,
    
    [Parameter(Mandatory=$true)]
    [string]$TeamName,
    
    [Parameter(Mandatory=$false)]
    [string]$ApiUrl = "http://localhost:5000",
    
    [Parameter(Mandatory=$false)]
    [switch]$Overwrite
)

# Validate folder exists
if (-not (Test-Path $BotFolder)) {
    Write-Error "Bot folder not found: $BotFolder"
    exit 1
}

Write-Host "🤖 Bot Submission Script" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Team Name: $TeamName" -ForegroundColor Yellow
Write-Host "Bot Folder: $BotFolder" -ForegroundColor Yellow
Write-Host "API URL: $ApiUrl" -ForegroundColor Yellow
Write-Host ""

# Get all .cs files recursively
$csFiles = Get-ChildItem -Path $BotFolder -Recurse -Filter "*.cs"

if ($csFiles.Count -eq 0) {
    Write-Error "No .cs files found in $BotFolder"
    exit 1
}

Write-Host "Found $($csFiles.Count) C# files:" -ForegroundColor Green

# Build the files array for the API
$files = @()
$basePath = (Resolve-Path $BotFolder).Path

foreach ($file in $csFiles) {
    $relativePath = $file.FullName.Replace($basePath + "\", "").Replace("\", "/")
    $code = Get-Content $file.FullName -Raw
    
    Write-Host "  + $relativePath ($($code.Length) bytes)" -ForegroundColor Gray
    
    $files += @{
        fileName = $relativePath
        code = $code
    }
}

# Build the request payload
$payload = @{
    teamName = $TeamName
    files = $files
    overwrite = $Overwrite.IsPresent
} | ConvertTo-Json -Depth 10

Write-Host ""
Write-Host "Submitting to API..." -ForegroundColor Cyan

try {
    # Submit to API
    $response = Invoke-RestMethod `
        -Uri "$ApiUrl/api/bots/submit" `
        -Method Post `
        -Body $payload `
        -ContentType "application/json" `
        -TimeoutSec 30
    
    if ($response.success) {
        Write-Host "[SUCCESS]" -ForegroundColor Green
        Write-Host "  Team: $($response.teamName)" -ForegroundColor Green
        Write-Host "  Message: $($response.message)" -ForegroundColor Green
        if ($response.warnings -and $response.warnings.Count -gt 0) {
            Write-Host "  Warnings:" -ForegroundColor Yellow
            foreach ($warning in $response.warnings) {
                Write-Host "    - $warning" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "[FAILED]" -ForegroundColor Red
        Write-Host "  Message: $($response.message)" -ForegroundColor Red
        if ($response.errors) {
            Write-Host "  Errors:" -ForegroundColor Red
            foreach ($error in $response.errors) {
                Write-Host "    - $error" -ForegroundColor Red
            }
        }
        exit 1
    }
}
catch {
    Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) {
        Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
    exit 1
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
