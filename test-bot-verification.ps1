# Test bot verification API endpoint with StrategicMind bot

$botRootDir = Join-Path $PSScriptRoot "TournamentEngine.Tests\TestData\StrategicMind\StrategicMind_Submission\StrategicMind_Submission"
$botRootDir = Resolve-Path $botRootDir

# Read all .cs and .md files
$files = @()

Write-Host "Reading C# files from: $botRootDir"
Get-ChildItem -Path $botRootDir -Filter "*.cs" -Recurse | ForEach-Object {
    $relativePath = $_.FullName.Substring($botRootDir.Path.Length + 1).Replace('\', '/')
    $content = Get-Content $_.FullName -Raw
    $files += @{
        fileName = $relativePath
        code = $content
    }
}

Write-Host "Reading Markdown files..."
Get-ChildItem -Path $botRootDir -Filter "*.md" -Recurse | ForEach-Object {
    $relativePath = $_.FullName.Substring($botRootDir.Path.Length + 1).Replace('\', '/')
    $content = Get-Content $_.FullName -Raw
    $files += @{
        fileName = $relativePath
        code = $content
    }
}

Write-Host "Found $($files.Count) files"
Write-Host "Files:"
$files | ForEach-Object { Write-Host "  - $($_.fileName)" }

# Create request payload
$payload = @{
    teamName = "StrategicMind"
    files = $files
} | ConvertTo-Json -Depth 10

# Send request
Write-Host ""
Write-Host "Sending verification request to http://localhost:5000/api/bots/verify..."
Write-Host ""

$response = Invoke-RestMethod -Uri "http://localhost:5000/api/bots/verify" `
    -Method POST `
    -ContentType "application/json" `
    -Body $payload

# Display results
Write-Host "Verification Result:"
Write-Host "==================="
Write-Host "Is Valid: $($response.isValid)"
Write-Host ""

if ($response.errors -and $response.errors.Count -gt 0) {
    Write-Host "ERRORS ($($response.errors.Count)):"
    $response.errors | ForEach-Object { Write-Host "  ❌ $_" }
    Write-Host ""
}

if ($response.warnings -and $response.warnings.Count -gt 0) {
    Write-Host "WARNINGS ($($response.warnings.Count)):"
    $response.warnings | ForEach-Object { Write-Host "  ⚠️  $_" }
    Write-Host ""
}

if (-not $response.errors -or $response.errors.Count -eq 0) {
    Write-Host "✅ No errors found!"
}

Write-Host ""
Write-Host "Summary:"
Write-Host "--------"
Write-Host "Errors: $($response.errors.Count)"
Write-Host "Warnings: $($response.warnings.Count)"
Write-Host "Valid: $($response.isValid)"
