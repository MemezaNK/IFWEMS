# Deploying IFWEMS to a Windows VPS (IIS + self-hosted GitHub Actions runner)

This is a one-time setup runbook. After completing it, every push to `main` automatically
publishes the API, builds the Angular client into it, and redeploys to IIS via
`.github/workflows/deploy.yml`.

Architecture: the Angular production build is copied into the API's `wwwroot` at publish time
(see the `BuildAngularApp` target in [src/IFWEMS.Api/IFWEMS.Api.csproj](../src/IFWEMS.Api/IFWEMS.Api.csproj)),
and `Program.cs` serves it as static files with a SPA fallback to `index.html`. So there is
**one IIS site, one process, one origin** — no CORS or reverse-proxy config needed. The
Angular app already calls a relative `/api` base URL (`environment.production.ts`), which
`api/[controller]` routes on the same origin satisfy directly.

## 1. Install prerequisites on the VPS

Run as Administrator (PowerShell):

```powershell
# IIS + required role features
Install-WindowsFeature -Name Web-Server, Web-Asp-Net45, Web-Net-Ext45, Web-App-Dev -IncludeManagementTools

# .NET 8 SDK (required for `dotnet publish`/`dotnet build`/`dotnet ef` — the Hosting Bundle
# below only installs the runtime, which is NOT enough to build/publish the app)
winget install Microsoft.DotNet.SDK.8

# .NET 8 Hosting Bundle (installs ASP.NET Core Module v2 for IIS + the runtime IIS uses to host the published app)
Invoke-WebRequest -Uri "https://dotnet.microsoft.com/download/dotnet/8.0" -OutFile "$env:TEMP\dotnet-hosting-8-win.exe"
# (Use the actual "Hosting Bundle" download link for the current 8.0.x release from
# https://dotnet.microsoft.com/download/dotnet/8.0 — direct links change per patch version.)
Start-Process "$env:TEMP\dotnet-hosting-8-win.exe" -ArgumentList "/quiet /norestart" -Wait

# Node.js LTS (needed because `dotnet publish` triggers `npm ci` / `ng build`)
winget install OpenJS.NodeJS.LTS

# Restart IIS so it picks up the new module
net stop was /y
net start w3svc
```

Install SQL Server (Developer/Standard/Express edition) natively if not already present, and
enable **Mixed Mode Authentication** (or use a dedicated Windows service account) so the app
can connect with a SQL login.

## 2. Create the database and app login

In SQL Server Management Studio or `sqlcmd` on the VPS:

```sql
CREATE LOGIN ifwems_app WITH PASSWORD = '<choose a strong password>';
CREATE DATABASE IfwemsDb;
GO
USE IfwemsDb;
CREATE USER ifwems_app FOR LOGIN ifwems_app;
ALTER ROLE db_datareader ADD MEMBER ifwems_app;
ALTER ROLE db_datawriter ADD MEMBER ifwems_app;
ALTER ROLE db_ddladmin ADD MEMBER ifwems_app;  -- needed once for EF migrations; can be revoked after
GO
```

Update `src/IFWEMS.Api/appsettings.Production.json`'s connection string (or, better, override it
via an environment variable — see step 5) with this login's password. **Never commit the real
password.**

## 3. Create the IIS site and application pool

```powershell
Import-Module WebAdministration

New-Item "IIS:\AppPools\IFWEMS" -Force
Set-ItemProperty "IIS:\AppPools\IFWEMS" -Name "managedRuntimeVersion" -Value ""  # No Managed Code — required for ASP.NET Core Module

New-Item "C:\inetpub\ifwems" -ItemType Directory -Force
New-Website -Name "IFWEMS" -PhysicalPath "C:\inetpub\ifwems" -ApplicationPool "IFWEMS" -Port 80

# Open the firewall for HTTP
New-NetFirewallRule -DisplayName "IFWEMS HTTP" -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow
```

Leave `C:\inetpub\ifwems` empty for now — the first CI run (or `deploy/deploy.ps1`) populates it.

## 4. Install a self-hosted GitHub Actions runner on the VPS

On GitHub: repo → **Settings → Actions → Runners → New self-hosted runner** (choose Windows) and
follow the generated `config.cmd` commands, e.g.:

```powershell
mkdir C:\actions-runner; cd C:\actions-runner
Invoke-WebRequest -Uri <download-url-from-github-ui> -OutFile actions-runner.zip
Expand-Archive actions-runner.zip -DestinationPath .
./config.cmd --url https://github.com/<org>/<repo> --token <token-from-github-ui>
./svc.cmd install
./svc.cmd start
```

The runner only makes outbound connections to GitHub — no inbound port needs to be opened for
CI/CD itself.

**Important:** install the .NET SDK and Node.js *before* installing the runner service (step 4),
or restart the runner service afterwards (`Restart-Service actions.runner.*` or
`./svc.cmd stop` then `./svc.cmd start` from the runner folder). Windows services only see the
`PATH` that existed when they started, so if `dotnet`/`node`/`npm` were installed after the
runner service, its jobs will fail with `dotnet : The term 'dotnet' is not recognized...` even
though `dotnet --version` works fine in an interactive PowerShell window. If it still can't find
`dotnet` after a service restart, reboot the VPS once to guarantee the machine-wide `PATH`
propagates to services, then restart the runner service again.

Also install the EF Core CLI tool globally so the workflow's migration step works:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.*
```

## 5. Configure secrets and variables in GitHub

Repo → **Settings → Secrets and variables → Actions**:

| Type | Name | Value |
|---|---|---|
| Secret | `PROD_CONNECTION_STRING` | `Server=localhost;Database=IfwemsDb;User Id=ifwems_app;Password=<the real password>;TrustServerCertificate=True;MultipleActiveResultSets=true` |
| Variable | `IIS_SITE_NAME` | `IFWEMS` |
| Variable | `IIS_APP_POOL_NAME` | `IFWEMS` |
| Variable | `IIS_SITE_PATH` | `C:\inetpub\ifwems` |
| Variable | `SITE_HEALTH_URL` | `http://<vps-ip>/health/ready` |

Also set the JWT signing key as a **machine-level environment variable** on the VPS (ASP.NET
Core config automatically maps `Jwt__Key` → `Jwt:Key`), so it never lives in source control:

```powershell
[Environment]::SetEnvironmentVariable("Jwt__Key", "<a long random secret, e.g. from `openssl rand -base64 48`>", "Machine")
# Restart the app pool afterwards so IIS picks up the new machine env var:
Restart-WebAppPool -Name "IFWEMS"
```

Consider also creating a `production` GitHub **Environment** (Settings → Environments) with a
required reviewer, so deploys need manual approval before running on the VPS.

## 6. First deployment

Push to `main` (or run the workflow manually via **Actions → Deploy to VPS (IIS) → Run workflow**).
The workflow will:
1. `dotnet publish` the API (this transitively runs `npm ci` + `ng build --configuration production`
   and copies the Angular output into `wwwroot`).
2. Apply pending EF Core migrations against the production database.
3. Stop the IIS site/app pool, mirror the publish output into `C:\inetpub\ifwems`, restart it.
4. Hit `/health/ready` as a smoke test.

For a manual one-off deploy without CI, run on the VPS:

```powershell
./deploy/deploy.ps1 -SitePath 'C:\inetpub\ifwems' -SiteName 'IFWEMS' -AppPoolName 'IFWEMS'
```

## 7. Later: adding a domain + HTTPS

Once a domain points at the VPS, install [win-acme](https://www.win-acme.com/) to obtain and
auto-renew a Let's Encrypt certificate bound to the IIS site, then set
`UseHttpsRedirection: true` in `appsettings.Production.json` and add an HTTPS binding to the
site.
