# TicketingSystem Docker Compose and NBomber Stress Testing Script

# Ensure script stops on first error
$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   TicketingSystem NBomber Stress Test" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Resolve repository root path
$RepoRoot = Resolve-Path "$PSScriptRoot\.."

# 1. Check Docker service status
Write-Host "[1/5] Checking Docker service status..." -ForegroundColor Yellow

$oldErrorAction = $ErrorActionPreference
$ErrorActionPreference = "SilentlyContinue"
& docker ps > $null 2>$null
$dockerExitCode = $LASTEXITCODE
$ErrorActionPreference = $oldErrorAction

if ($dockerExitCode -ne 0) {
    Write-Error "Docker is not running or not accessible. Please start Docker Desktop first!"
    exit 1
}
Write-Host "-> Docker daemon is active." -ForegroundColor Green

# 2. Build and start API and Redis containers
Write-Host "[2/5] Starting all services via Docker Compose (docker compose up -d --build)..." -ForegroundColor Yellow
try {
    Push-Location "$RepoRoot\TicketingSystem"
    & docker compose up -d --build
    $composeExit = $LASTEXITCODE
    Pop-Location
    if ($composeExit -ne 0) {
        throw "Failed to start docker compose services."
    }
    Write-Host "-> Docker Compose services are running in background." -ForegroundColor Green
}
catch {
    Write-Error "Failed to start Docker Compose services!"
    exit 1
}

# 3. Wait for the API to be fully online and ready
Write-Host "[3/5] Waiting for API on port 8080 to be healthy and ready..." -ForegroundColor Yellow
$retryCount = 0
$maxRetries = 30
$apiReady = $false

$oldErrorAction = $ErrorActionPreference
$ErrorActionPreference = "SilentlyContinue"

while (-not $apiReady -and $retryCount -lt $maxRetries) {
    try {
        # Using empty list POST to /init as a health check and initial DB/Redis cleanup
        $response = Invoke-RestMethod -Uri "http://localhost:8080/init" -Method Post -Body '[]' -ContentType "application/json" -TimeoutSec 2
        $apiReady = $true
    }
    catch {
        $retryCount++
        Write-Host "-> API is starting up... (Attempt $retryCount/$maxRetries)" -ForegroundColor Gray
        Start-Sleep -Seconds 3
    }
}

$ErrorActionPreference = $oldErrorAction

if (-not $apiReady) {
    Write-Error "Timeout: API on port 8080 did not become ready in time."
    exit 1
}
Write-Host "-> API is online and healthy!" -ForegroundColor Green

# 4. Initialize Concert Ticket Stock
Write-Host "[4/5] Initializing ticket stock (100,000 tickets for Concert 'C101' and Area 'A1')..." -ForegroundColor Yellow
try {
    $initPayload = '[{"ConcertId": "C101", "AreaId": "A1", "TotalTickets": 100000}]'
    $response = Invoke-RestMethod -Uri "http://localhost:8080/init" -Method Post -Body $initPayload -ContentType "application/json"
    Write-Host "-> Concert and area successfully initialized." -ForegroundColor Green
}
catch {
    Write-Error "Failed to initialize ticket stock on the API!"
    exit 1
}

# 5. Run NBomber Stress Tests
Write-Host "[5/5] Launching NBomber Stress Test scenario..." -ForegroundColor Yellow
try {
    Push-Location $PSScriptRoot
    & dotnet run
    $stressExitCode = $LASTEXITCODE
    Pop-Location
}
catch {
    $stressExitCode = 1
}

if ($stressExitCode -eq 0) {
    Write-Host "==========================================" -ForegroundColor Green
    Write-Host "   Stress Testing Completed: SUCCESS!" -ForegroundColor Green
    Write-Host "==========================================" -ForegroundColor Green
} else {
    Write-Host "==========================================" -ForegroundColor Red
    Write-Host "   Stress Testing Failed! (Exit Code: $stressExitCode)" -ForegroundColor Red
    Write-Host "==========================================" -ForegroundColor Red
}

Write-Host "=================END=========================" -ForegroundColor Green
PAUSE
