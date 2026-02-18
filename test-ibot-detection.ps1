# Test IBot detection with actual bot file
$botFile = "TournamentEngine.Tests\TestData\StrategicMind\StrategicMind_Submission\StrategicMind_Submission\BotCode\StrategicMindBot.cs"
$code = Get-Content $botFile -Raw

Write-Host "File: $botFile"
Write-Host "Code length: $($code.Length)"
Write-Host ""

# Test the exact regex used in the API
$pattern = ":\s*IBot\b"
$match = [regex]::IsMatch($code, $pattern)

Write-Host "Regex pattern: $pattern"
Write-Host "Match result: $match"
Write-Host ""

# Find the actual match
if ($match) {
    $matchObj = [regex]::Match($code, $pattern)
    Write-Host "Match found at position: $($matchObj.Index)"
    Write-Host "Match value: '$($matchObj.Value)'"
    
    # Show context around the match
    $start = [Math]::Max(0, $matchObj.Index - 30)
    $length = [Math]::Min(100, $code.Length - $start)
    $context = $code.Substring($start, $length)
    Write-Host "Context: $context"
} else {
    Write-Host "NO MATCH FOUND!"
    Write-Host ""
    Write-Host "Searching for 'IBot' in code..."
    if ($code.Contains("IBot")) {
        Write-Host "  - Code DOES contain 'IBot'"
        $index = $code.IndexOf("IBot")
        Write-Host "  - First occurrence at position: $index"
        $start = [Math]::Max(0, $index - 30)
        $context = $code.Substring($start, 60)
        Write-Host "  - Context: $context"
    } else {
        Write-Host "  - Code does NOT contain 'IBot' at all!"
    }
}

# Test with JSON encoding (as it would be sent to API)
Write-Host ""
Write-Host "Testing with JSON encoding..."
$json = @{
    fileName = "StrategicMindBot.cs"
    code = $code
} | ConvertTo-Json

$decoded = ($json | ConvertFrom-Json).code
$matchDecoded = [regex]::IsMatch($decoded, $pattern)
Write-Host "Match after JSON round-trip: $matchDecoded"
