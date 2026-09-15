#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the IFWEMS API and Angular client together for local development/demo.
#>

$repoRoot = $PSScriptRoot
$apiProject = Join-Path $repoRoot "src/IFWEMS.Api"
$clientDir = Join-Path $repoRoot "src/IFWEMS.Client"

Write-Host "Starting IFWEMS API (https://localhost:7101, http://localhost:5092)..." -ForegroundColor Cyan
$api = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", $apiProject, "--launch-profile", "https" -PassThru -WindowStyle Normal

Write-Host "Starting IFWEMS Angular client (http://localhost:4200)..." -ForegroundColor Cyan
$client = Start-Process -FilePath "npm" -ArgumentList "start" -WorkingDirectory $clientDir -PassThru -WindowStyle Normal

Write-Host ""
Write-Host "API PID: $($api.Id)  |  Client PID: $($client.Id)" -ForegroundColor Green
Write-Host "Swagger: https://localhost:7101/swagger" -ForegroundColor Green
Write-Host "Client:  http://localhost:4200" -ForegroundColor Green
Write-Host "Demo login: admin / Admin@12345 (see db/seed/03_demo_data.sql for more accounts)" -ForegroundColor Green
Write-Host ""
Write-Host "Press Ctrl+C in this window to stop both processes." -ForegroundColor Yellow

try {
    Wait-Process -Id $api.Id, $client.Id
}
finally {
    Write-Host "Shutting down..." -ForegroundColor Yellow
    Stop-Process -Id $api.Id -ErrorAction SilentlyContinue
    Stop-Process -Id $client.Id -ErrorAction SilentlyContinue
}
