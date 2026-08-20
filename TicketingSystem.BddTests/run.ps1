# TicketingSystem BDD Test and Docker Automation Script

# Ensure script stops on first error
$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Starting Docker Redis and BDD Tests" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Resolve repository root path
$RepoRoot = Resolve-Path "$PSScriptRoot\.."

# 1. Check Docker service status
Write-Host "[1/3] Checking Docker service status..." -ForegroundColor Yellow

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

# 2. Start Redis container
Write-Host "[2/3] Starting Redis service (docker compose up -d redis)..." -ForegroundColor Yellow
try {
    # Start only the redis container from the nested TicketingSystem directory
    Push-Location "$RepoRoot\TicketingSystem"
    & docker compose up -d redis
    $composeExit = $LASTEXITCODE
    Pop-Location
    
    if ($composeExit -ne 0) {
        throw "Failed to start Redis container."
    }
    Write-Host "-> Redis service started in background." -ForegroundColor Green
    
    # Wait 3 seconds to ensure Redis is fully ready
    Write-Host "-> Waiting 3 seconds for Redis to initialize..." -ForegroundColor Gray
    Start-Sleep -Seconds 3
}
catch {
    Write-Error "Failed to spin up Redis service!"
    exit 1
}

# 3. Run BDD Tests
Write-Host "[3/3] Running BDD Tests..." -ForegroundColor Yellow

try {
    # Pass all script arguments to dotnet test
    Push-Location $PSScriptRoot
    & dotnet test --nologo @args
    $testExitCode = $LASTEXITCODE
    Pop-Location
}
catch {
    $testExitCode = 1
}

if ($testExitCode -eq 0) {
    Write-Host "==========================================" -ForegroundColor Green
    Write-Host "   BDD Tests Completed: ALL PASSED!" -ForegroundColor Green
    Write-Host "==========================================" -ForegroundColor Green
} else {
    Write-Host "==========================================" -ForegroundColor Red
    Write-Host "   BDD Tests Completed: SOME TESTS FAILED! (Exit Code: $testExitCode)" -ForegroundColor Red
    Write-Host "==========================================" -ForegroundColor Red
}

# Return the exit code of dotnet test
Write-Host "================= END =========================" -ForegroundColor Red
PAUSE
exit $testExitCode
