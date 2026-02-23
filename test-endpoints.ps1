#!/usr/bin/env pwsh

Write-Host "=== TESTING 6 REQUIRED ENDPOINTS ===" -ForegroundColor Cyan
Write-Host ""

# 1. Tournament Status
Write-Host "1. GET /api/tournament-engine/status" -ForegroundColor Yellow
try {
    $resp = Invoke-WebRequest -Uri "http://localhost:8080/api/tournament-engine/status" -UseBasicParsing
    Write-Host "Status Code: $($resp.StatusCode)" -ForegroundColor Green
    Write-Host "Response Body:" -ForegroundColor Gray
    Write-Host ($resp.Content | ConvertFrom-Json |  ConvertTo-Json -Depth 3)
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 2. Tournament Events
Write-Host "2. GET /api/tournament-engine/events" -ForegroundColor Yellow
try {
    $resp = Invoke-WebRequest -Uri "http://localhost:8080/api/tournament-engine/events" -UseBasicParsing
    Write-Host "Status Code: $($resp.StatusCode)" -ForegroundColor Green
    $json = $resp.Content | ConvertFrom-Json
    Write-Host "Count: $($json.Count) events" -ForegroundColor Green
    Write-Host "Response Body:" -ForegroundColor Gray
    Write-Host ($json | ConvertTo-Json -Depth 3)
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 3. Overall Leaders
Write-Host "3. GET /api/tournament-engine/leaders" -ForegroundColor Yellow
try {
    $resp = Invoke-WebRequest -Uri "http://localhost:8080/api/tournament-engine/leaders" -UseBasicParsing
    Write-Host "Status Code: $($resp.StatusCode)" -ForegroundColor Green
    $json = $resp.Content | ConvertFrom-Json
    Write-Host "Count: $($json.Count) leaders" -ForegroundColor Green
    if ($json.Count -gt 0) {
        Write-Host "Top 3:" -ForegroundColor Gray
        Write-Host ($json | Select-Object -First 3 | ConvertTo-Json -Depth 2)
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 4. Groups by Event
Write-Host "4. GET /api/tournament-engine/groups/RPSLS" -ForegroundColor Yellow
try {
    $resp = Invoke-WebRequest -Uri "http://localhost:8080/api/tournament-engine/groups/RPSLS" -UseBasicParsing
    Write-Host "Status Code: $($resp.StatusCode)" -ForegroundColor Green
    $json = $resp.Content | ConvertFrom-Json
    Write-Host "Count: $($json.Count) groups" -ForegroundColor Green
    if ($json.Count -gt 0) {
        Write-Host "First group:" -ForegroundColor Gray
        Write-Host ($json | Select-Object -First 1 | ConvertTo-Json -Depth 2)
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red  
}
Write-Host ""

# 5. Group Details
Write-Host "5. GET /api/tournament-engine/groups/RPSLS/Group%20A" -ForegroundColor Yellow
try {
    $resp = Invoke-WebRequest -Uri "http://localhost:8080/api/tournament-engine/groups/RPSLS/Group%20A" -UseBasicParsing
    Write-Host "Status Code: $($resp.StatusCode)" -ForegroundColor Green
    $json = $resp.Content | ConvertFrom-Json
    Write-Host "Response Body:" -ForegroundColor Gray
    Write-Host ($json | ConvertTo-Json -Depth 3)
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 6. Connection Status
Write-Host "6. GET /api/tournament-engine/connection" -ForegroundColor Yellow
try {
    $resp = Invoke-WebRequest -Uri "http://localhost:8080/api/tournament-engine/connection" -UseBasicParsing
    Write-Host "Status Code: $($resp.StatusCode)" -ForegroundColor Green
    Write-Host "Response Body:" -ForegroundColor Gray
    Write-Host ($resp.Content | ConvertFrom-Json | ConvertTo-Json -Depth 3)
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
