# Extract and verify StrategicMind bot from zip file
$zipPath = "C:\Users\saplizki\Downloads\StrategicMind_Submission.zip"
$extractPath = "$env:TEMP\StrategicMind_Test_$(Get-Date -Format 'yyyyMMddHHmmss')"

Write-Host "Extracting $zipPath..." -ForegroundColor Cyan
Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force

# Find all .cs and .md files
$files = @()
Get-ChildItem -Path $extractPath -Include *.cs,*.md,*.csproj -Recurse | ForEach-Object {
    $relativePath = $_.FullName.Substring($extractPath.Length + 1).Replace('\', '/')
    $content = Get-Content $_.FullName -Raw
    
    Write-Host "  Found: $relativePath ($($content.Length) chars)" -ForegroundColor Gray
    
    $files += @{
        fileName = $relativePath
        code = $content
    }
}

Write-Host ""
Write-Host "Total files: $($files.Count)" -ForegroundColor Green
Write-Host ""

# Build request payload
$payload = @{
    teamName = "StrategicMind"
    files = $files
} | ConvertTo-Json -Depth 10

# Send to API
$apiUrl = "http://localhost:8090/api/bots/verify"
Write-Host "Sending verification request to $apiUrl..." -ForegroundColor Cyan
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri $apiUrl -Method POST -Body $payload -ContentType "application/json" -TimeoutSec 60
    
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host "VERIFICATION RESULT" -ForegroundColor Yellow
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Is Valid: $($response.isValid)" -ForegroundColor $(if ($response.isValid) { "Green" } else { "Red" })
    Write-Host ""
    
    if ($response.errors -and $response.errors.Count -gt 0) {
        Write-Host "ERRORS ($($response.errors.Count)):" -ForegroundColor Red
        $response.errors | ForEach-Object { Write-Host "  ❌ $_" -ForegroundColor Red }
        Write-Host ""
    }
    
    if ($response.warnings -and $response.warnings.Count -gt 0) {
        Write-Host "WARNINGS ($($response.warnings.Count)):" -ForegroundColor Yellow
        $response.warnings | ForEach-Object { Write-Host "  ⚠️  $_" -ForegroundColor Yellow }
        Write-Host ""
    }
    
    if ($response.isValid) {
        Write-Host "✅ BOT VERIFICATION SUCCESSFUL!" -ForegroundColor Green
    } else {
        Write-Host "❌ BOT VERIFICATION FAILED" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "Message: $($response.message)" -ForegroundColor Cyan
    
} catch {
    Write-Host "ERROR: Failed to verify bot" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host ""
        Write-Host "Details:" -ForegroundColor Yellow
        Write-Host $_.ErrorDetails.Message
    }
}

# Cleanup
Remove-Item -Path $extractPath -Recurse -Force -ErrorAction SilentlyContinue
