#!/usr/bin/env pwsh

Write-Host "=== TESTING ENDPOINTS WITH SAMPLE DATA ===" -ForegroundColor Cyan
Write-Host ""

$dashboardUri = "http://localhost:8080"

# Sample match data to inject
$sampleMatches = @(
    @{
        MatchId = "match-1"
        EventName = "RPSLS"
        GroupLabel = "Group A"
        Bot1Name = "AlphaBot"
        Bot2Name = "BetaBot"
        Bot1Score = 10
        Bot2Score = 5
        WinnerName = "AlphaBot"
        Outcome = "Player1Wins"
        GameType = "RPSLS"
        CompletedAt = (Get-Date).AddMinutes(-10).ToUniversalTime().ToString("o")
        TournamentName = "TestTournament"
        TournamentId = "tournament-1"
        EventId = "event-1"
        GroupId = "group-a"
    },
    @{
        MatchId = "match-2"
        EventName = "RPSLS"
        GroupLabel = "Group A"
        Bot1Name = "GammaBot"
        Bot2Name = "AlphaBot"
        Bot1Score = 3
        Bot2Score = 8
        WinnerName = "AlphaBot"
        Outcome = "Player2Wins"
        GameType = "RPSLS"
        CompletedAt = (Get-Date).AddMinutes(-5).ToUniversalTime().ToString("o")
        TournamentName = "TestTournament"
        TournamentId = "tournament-1"
        EventId = "event-1"
        GroupId = "group-a"
    },
    @{
        MatchId = "match-3"
        EventName = "RPSLS"
        GroupLabel = "Group B"
        Bot1Name = "DeltaBot"
        Bot2Name = "EpsilonBot"
        Bot1Score = 7
        Bot2Score = 7
        WinnerName = $null
        Outcome = "Draw"
        GameType = "RPSLS"
        CompletedAt = (Get-Date).AddMinutes(-2).ToUniversalTime().ToString("o")
        TournamentName = "TestTournament"
        TournamentId = "tournament-1"
        EventId = "event-1"
        GroupId = "group-b"
    }
)

# Inject sample matches
Write-Host "Injecting sample match data..." -ForegroundColor Yellow
foreach ($match in $sampleMatches) {
    try {
        $json = $match | ConvertTo-Json
        $uri = "$dashboardUri/api/tournament-engine/match-result"
        $response = Invoke-WebRequest -Uri $uri -Method Post -Body $json -ContentType "application/json" -UseBasicParsing
        Write-Host "Match $($match.MatchId) injected (Status: $($response.StatusCode))" -ForegroundColor Green
    }
    catch {
        Write-Host "Failed to inject match $($match.MatchId): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== TESTING ENDPOINTS WITH DATA ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Get groups for RPSLS event
Write-Host "1. GET /api/tournament-engine/groups/RPSLS" -ForegroundColor Yellow
try {
    $resp = Invoke-WebRequest -Uri "$dashboardUri/api/tournament-engine/groups/RPSLS" -UseBasicParsing
    Write-Host "Status Code: $($resp.StatusCode)" -ForegroundColor Green
    $json = $resp.Content | ConvertFrom-Json
    Write-Host "Groups found: $($json.Count)" -ForegroundColor Green
    $json | ConvertTo-Json -Depth 2
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 2: Get matches for Group A
Write-Host "2. GET /api/tournament-engine/groups/RPSLS/Group%20A" -ForegroundColor Yellow
try {
    $resp = Invoke-WebRequest -Uri "$dashboardUri/api/tournament-engine/groups/RPSLS/Group%20A" -UseBasicParsing
    Write-Host "Status Code: $($resp.StatusCode)" -ForegroundColor Green
    $json = $resp.Content | ConvertFrom-Json
    Write-Host "Standings: $($json.groupStanding.Count), Matches: $($json.recentMatches.Count)" -ForegroundColor Green
    $json | ConvertTo-Json -Depth 3
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 3: Get matches for Group B
Write-Host "3. GET /api/tournament-engine/groups/RPSLS/Group%20B" -ForegroundColor Yellow
try {
    $resp = Invoke-WebRequest -Uri "$dashboardUri/api/tournament-engine/groups/RPSLS/Group%20B" -UseBasicParsing
    Write-Host "Status Code: $($resp.StatusCode)" -ForegroundColor Green
    $json = $resp.Content | ConvertFrom-Json
    Write-Host "Standings: $($json.groupStanding.Count), Matches: $($json.recentMatches.Count)" -ForegroundColor Green
    $json | ConvertTo-Json -Depth 3
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 4: Get all matches
Write-Host "4. GET /api/tournament-engine/matches" -ForegroundColor Yellow
try {
    $resp = Invoke-WebRequest -Uri "$dashboardUri/api/tournament-engine/matches" -UseBasicParsing
    Write-Host "Status Code: $($resp.StatusCode)" -ForegroundColor Green
    $json = $resp.Content | ConvertFrom-Json
    Write-Host "Total matches: $($json.Count)" -ForegroundColor Green
    $json | ConvertTo-Json -Depth 2
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "=== TEST COMPLETE ===" -ForegroundColor Cyan
