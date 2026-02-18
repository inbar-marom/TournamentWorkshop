# Simple bot verification test
$apiUrl = "http://localhost:5000/api/bots/verify"

# Create minimal test payload with just the bot file and one helper
$payload = @{
    teamName = "StrategicMindTest"
    files = @(
        @{
            fileName = "BotCode/StrategicMindBot.cs"
            code = (Get-Content "TournamentEngine.Tests\TestData\StrategicMind\StrategicMind_Submission\StrategicMind_Submission\BotCode\StrategicMindBot.cs" -Raw)
        },
        @{
            fileName = "BotCode/Core/GameRules.cs"
            code = (Get-Content "TournamentEngine.Tests\TestData\StrategicMind\StrategicMind_Submission\StrategicMind_Submission\BotCode\Core\GameRules.cs" -Raw)
        },
        @{
            fileName = "Config/ResearchAgent.agent.md"
            code = (Get-Content "TournamentEngine.Tests\TestData\StrategicMind\StrategicMind_Submission\StrategicMind_Submission\Config\ResearchAgent.agent.md" -Raw)
        }
    )
} | ConvertTo-Json -Depth 10

Write-Host "Sending verification request..."
try {
    $response = Invoke-RestMethod -Uri $apiUrl -Method POST -Body $payload -ContentType "application/json"
    
    Write-Host "`nVerification Result:"
    Write-Host "===================="
    Write-Host "Valid: $($response.isValid)"
    
    if ($response.errors) {
        Write-Host "`nErrors ($($response.errors.Count)):"
        $response.errors | ForEach-Object { Write-Host "  X $_" }
    }
    
    if ($response.warnings) {
        Write-Host "`nWarnings ($($response.warnings.Count)):"
        $response.warnings | ForEach-Object { Write-Host "  ! $_" }
    }
    
    if ($response.isValid) {
        Write-Host "`n✓ Bot verification successful!" -ForegroundColor Green
    } else {
        Write-Host "`n✗ Bot verification failed" -ForegroundColor Red
    }
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host $_.Exception.Message
}
