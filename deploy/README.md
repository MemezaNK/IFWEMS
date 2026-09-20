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
# below only installs the runtime, which is NOT enough to build/publish the app).
# IMPORTANT: verify this actually installs an 8.x SDK — `winget install Microsoft.DotNet.SDK.8`
# has been observed installing a newer major version (e.g. 10.x) on some machines instead.
# Run `dotnet --list-sdks` afterwards and confirm an 8.0.x entry is present. A newer SDK's
# build engine is NOT compatible with the older Microsoft.EntityFrameworkCore.Design package
# this repo uses — it causes `dotnet ef` to fail with "Missing required option '--assembly'".
# The repo's global.json pins to 8.x so builds fail fast with a clear "no SDK found" error
# instead, if only a newer major SDK is present.
winget install Microsoft.DotNet.SDK.8
dotnet --list-sdks   # confirm an 8.0.x line appears

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
runner service, its jobs will fail with `dotnet : The term 'dotnet' is not recognized...` or
`'npm' is not recognized as an internal or external command...` even though `dotnet --version`/
`npm --version` work fine in an interactive PowerShell window. **Every time you install or
upgrade dotnet/Node after the runner service already exists, you must restart the runner
service** for it to see the new `PATH`. If it still can't find the command after a service
restart, reboot the VPS once to guarantee the machine-wide `PATH` propagates to services, then
start the runner service again.

The workflow restores `dotnet-ef` automatically from the repo's local tool manifest
([.config/dotnet-tools.json](../.config/dotnet-tools.json)) via `dotnet tool restore` — no
global install needed. (A `dotnet tool install --global` would only be visible to whichever
Windows account owns that install; since the runner service typically runs as a system/service
account rather than your interactive user, a global install is unreliable here — the local
manifest avoids that problem entirely.)

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

## Troubleshooting: `dotnet ef` fails with "Missing required option '--assembly'"

This has shown up twice while standing up this pipeline, from two different causes -- check
both if it happens again:

1. **`ConnectionStrings__DefaultConnection` is empty.** The "Apply EF Core migrations" step
   now fails fast with a clear message if this happens, instead of letting `dotnet-ef` fail
   confusingly on `--assembly`. If you see the fail-fast message, the `PROD_CONNECTION_STRING`
   secret isn't set (or isn't set where the job can see it -- repo-level secrets, or the
   `production` environment's own secrets under **Settings > Environments > production**).
2. **A newer major .NET SDK is present on the VPS alongside 8.x** (see the note in step 1
   above). `dotnet --list-sdks` now runs as part of the migrations step so its output is in
   the log; confirm an `8.0.x` entry is there. If a 9.x/10.x SDK is also installed, the
   `global.json` pin should make plain `dotnet build`/`publish` fail loudly instead of picking
   it up -- but `dotnet-ef`'s own internal design-time build step has been the one to hit this
   inconsistently. Uninstalling the extra SDK (or moving it off `PATH` for the runner service
   account) is the reliable fix.

The migrations step also now passes `--configuration Release` (matching the `dotnet publish`
step that runs just before it) and `--verbose`, since running `dotnet-ef` with an implicit
`Debug` configuration against a checkout whose `obj/` cache was last evaluated for `Release`
is the most concrete reproduction of this error found so far.

## Troubleshooting: smoke test fails with "HTTP Error 500.19 - Internal Server Error" (Config Error 0x8007000d)

This means IIS could not read `web.config` because it doesn't recognize the
`AspNetCoreModuleV2` handler referenced in it (the `web.config` itself is auto-generated by
`dotnet publish` and is not checked into this repo). Almost always this means the **.NET
Hosting Bundle** (see step 1 above) is not installed on this VPS, or IIS was never restarted
after it was installed, so ASP.NET Core Module v2 was never registered with IIS.

Fix, on the VPS itself:

1. Install (or re-install) the **ASP.NET Core Hosting Bundle** for .NET 8 from
   https://dotnet.microsoft.com/download/dotnet/8.0 (the "Hosting Bundle" installer, not the
   SDK or the runtime alone).
2. Restart IIS so it picks up the newly-registered module:
   ```powershell
   net stop was /y
   net start w3svc
   ```
3. Confirm the module is present:
   ```powershell
   Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
   ```
   should return `True`.
4. Re-run the deploy workflow (or push a new commit).

The workflow now has a "Verify ASP.NET Core Hosting Bundle is installed" step that checks for
this up front and fails fast with the same guidance, instead of only surfacing the problem at
the smoke-test step after the DB migration and file copy have already run.
