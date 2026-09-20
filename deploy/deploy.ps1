#requires -Modules WebAdministration
<#
.SYNOPSIS
    Manually publishes IFWEMS and deploys it to a local IIS site. Use this for a one-off
    redeploy on the VPS, or as a reference for what the GitHub Actions workflow automates.

.PARAMETER SitePath
    Physical path IIS serves the site from (IIS site's physical path).

.PARAMETER SiteName
    IIS site name (as shown in `Get-Website`).

.PARAMETER AppPoolName
    IIS application pool name backing the site.

.EXAMPLE
    ./deploy/deploy.ps1 -SitePath 'C:\inetpub\ifwems' -SiteName 'IFWEMS' -AppPoolName 'IFWEMS'
#>
param(
    [Parameter(Mandatory = $true)][string]$SitePath,
    [Parameter(Mandatory = $true)][string]$SiteName,
    [Parameter(Mandatory = $true)][string]$AppPoolName
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $env:TEMP "ifwems-publish"

Write-Host "Publishing API + Angular client..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $repoRoot "src/IFWEMS.Api/IFWEMS.Api.csproj") -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "Stopping IIS site '$SiteName' and app pool '$AppPoolName'..." -ForegroundColor Cyan
Import-Module WebAdministration
Stop-WebSite -Name $SiteName -ErrorAction SilentlyContinue
Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue

Write-Host "Copying published output to $SitePath..." -ForegroundColor Cyan
robocopy $publishDir $SitePath /MIR /NFL /NDL /NJH
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }

Write-Host "Starting IIS site and app pool..." -ForegroundColor Cyan
Start-WebAppPool -Name $AppPoolName
Start-WebSite -Name $SiteName

Write-Host "Done. Site deployed to $SitePath" -ForegroundColor Green
