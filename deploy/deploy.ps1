#requires -Modules WebAdministration
<#
.SYNOPSIS
    Manually publishes one of the two applications in this repo (IFWEMS or TETA) and deploys it
    to its own IIS Application. Use this for a one-off redeploy on the VPS, or as a reference for
    what the GitHub Actions workflow (.github/workflows/deploy.yml) automates.

    Only the named app's own application pool is stopped/started -- the parent IIS site and the
    other app's pool are left running throughout, so the two applications stay independent of
    each other exactly as they are in production.

.PARAMETER App
    Which application to deploy: 'IFWEMS' or 'TETA'.

.PARAMETER SitePath
    Physical path of that application's own folder (its IIS Application's physical path -- e.g.
    'C:\inetpub\portal\IFWEMS' or 'C:\inetpub\portal\TETA'; NOT the parent site's folder).

.PARAMETER AppPoolName
    IIS application pool name backing that application (its own dedicated pool).

.EXAMPLE
    ./deploy/deploy.ps1 -App IFWEMS -SitePath 'C:\inetpub\portal\IFWEMS' -AppPoolName 'IFWEMS'
.EXAMPLE
    ./deploy/deploy.ps1 -App TETA -SitePath 'C:\inetpub\portal\TETA' -AppPoolName 'TETA'
#>
param(
    [Parameter(Mandatory = $true)][ValidateSet('IFWEMS', 'TETA')][string]$App,
    [Parameter(Mandatory = $true)][string]$SitePath,
    [Parameter(Mandatory = $true)][string]$AppPoolName
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $env:TEMP "$($App.ToLower())-publish"
$csproj = if ($App -eq 'IFWEMS') { Join-Path $repoRoot "src/IFWEMS.Api/IFWEMS.Api.csproj" } else { Join-Path $repoRoot "src/Teta.Ippcms.Api/Teta.Ippcms.Api.csproj" }

Write-Host "Publishing $App (API + Angular client)..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $csproj -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "Stopping app pool '$AppPoolName'..." -ForegroundColor Cyan
Import-Module WebAdministration
Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue

Write-Host "Copying published output to $SitePath..." -ForegroundColor Cyan
robocopy $publishDir $SitePath /MIR /NFL /NDL /NJH
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }

Write-Host "Starting app pool '$AppPoolName'..." -ForegroundColor Cyan
Start-WebAppPool -Name $AppPoolName

Write-Host "Done. $App deployed to $SitePath" -ForegroundColor Green
