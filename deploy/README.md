# Deploying IFWEMS + TETA to a Windows VPS (IIS + self-hosted GitHub Actions runner)

This is a one-time setup runbook. After completing it, every push to `main` automatically
publishes **both** applications in this repo, builds each one's own Angular client into it, and
redeploys both to IIS via `.github/workflows/deploy.yml`.

## Architecture

This repo contains two independent web applications that share one server, one database and a
few platform-level concerns (security/authentication, audit trail, notifications), but otherwise
run as separate processes with separate URLs:

| Application | Project | URL | IIS Site | App pool |
|---|---|---|---|---|
| IFWEMS (existing) | `src/IFWEMS.Api` | `http://<vps-ip>:8083/` | `IFWEMS` | `IFWEMS` |
| TETA-IPPCMS (new) | `src/Teta.Ippcms.Api` | `http://<vps-ip>:8082/` | `TETA` | `TETA` |

**Each app is its own standalone IIS site on its own port**, serving from its own site root
(base href `/`) — not path-based sub-Applications under one shared site/port. This VPS already
runs an unrelated system (`iTrack-API`/`iTrack-UI`) that occupies port 80, so a single shared
"Portal" site on port 80 (an earlier version of this runbook's design) isn't available here;
each app instead gets its own dedicated port, opened individually in the firewall (see step 1).
Each application's own Angular production build is copied into that application's `wwwroot` at
publish time (see the `BuildAngularApp` target in
[src/IFWEMS.Api/IFWEMS.Api.csproj](../src/IFWEMS.Api/IFWEMS.Api.csproj) and
[src/Teta.Ippcms.Api/Teta.Ippcms.Api.csproj](../src/Teta.Ippcms.Api/Teta.Ippcms.Api.csproj)), and
each `Program.cs` serves it as static files with a SPA fallback to `index.html`. Each Angular
client is built with base href `/` and calls a relative `api` base URL
(`environment.production.ts`), which resolves on that app's own origin — so there is **no CORS
and no reverse-proxy config** for either app, just two distinct ports.

Because each application is its **own IIS site with its own dedicated app pool**, deploying or
recycling one never stops or restarts the other's worker process — they run independently,
exactly as required. Both
connect to the **same SQL Server database**: IFWEMS owns the shared tables in the default `dbo`
schema (including `dbo.Users`, the shared identity/login store), while every TETA-specific table
lives in its own `teta` schema (created automatically by TETA's first EF Core migration). TETA
reads/writes `dbo.Users` for shared login and user look-ups but does not migrate it — IFWEMS
remains the owner of that table. The two apps do **not** share a session/token: each issues and
validates its **own** JWT (different `Issuer`/`Audience`/signing key — see each app's
`appsettings.Production.json`), so signing in to one does not sign you in to the other.

## 1. Install prerequisites on the VPS

Run as Administrator (PowerShell):

```powershell
# IIS + required role features
Install-WindowsFeature -Name Web-Server, Web-Asp-Net45, Web-Net-Ext45, Web-App-Dev -IncludeManagementTools

# .NET 8 SDK (required for `dotnet publish`/`dotnet build`/`dotnet ef` — the Hosting Bundle
# below only installs the runtime, which is NOT enough to build/publish either app).
# IMPORTANT: verify this actually installs an 8.x SDK — `winget install Microsoft.DotNet.SDK.8`
# has been observed installing a newer major version (e.g. 10.x) on some machines instead.
# Run `dotnet --list-sdks` afterwards and confirm an 8.0.x entry is present. A newer SDK's
# build engine is NOT compatible with the older Microsoft.EntityFrameworkCore.Design package
# this repo uses — it causes `dotnet ef` to fail with "Missing required option '--assembly'".
# The repo's global.json pins to 8.x so builds fail fast with a clear "no SDK found" error
# instead, if only a newer major SDK is present.
winget install Microsoft.DotNet.SDK.8
dotnet --list-sdks   # confirm an 8.0.x line appears

# .NET 8 Hosting Bundle (installs ASP.NET Core Module v2 for IIS + the runtime IIS uses to host
# BOTH published apps)
Invoke-WebRequest -Uri "https://dotnet.microsoft.com/download/dotnet/8.0" -OutFile "$env:TEMP\dotnet-hosting-8-win.exe"
# (Use the actual "Hosting Bundle" download link for the current 8.0.x release from
# https://dotnet.microsoft.com/download/dotnet/8.0 — direct links change per patch version.)
Start-Process "$env:TEMP\dotnet-hosting-8-win.exe" -ArgumentList "/quiet /norestart" -Wait

# Node.js LTS (needed because `dotnet publish` triggers `npm ci` / `ng build` for each app's
# Angular client)
winget install OpenJS.NodeJS.LTS

# Restart IIS so it picks up the new module
net stop was /y
net start w3svc
```

Install SQL Server (Developer/Standard/Express edition) natively if not already present, and
enable **Mixed Mode Authentication** (or use a dedicated Windows service account) so the apps can
connect with a SQL login.

## 2. Create the database and app login

Both applications use the **same** database and the **same** SQL login (granted `db_datareader`/
`db_datawriter`/`db_ddladmin` at the database level, not scoped to one schema) — IFWEMS's
migrations create and own the `dbo` tables, TETA's migrations create and own the `teta` schema and
its tables, and the shared login can read/write both. In SQL Server Management Studio or `sqlcmd`
on the VPS:

```sql
CREATE LOGIN ifwems_app WITH PASSWORD = '<choose a strong password>';
CREATE DATABASE IfwemsDb;
GO
USE IfwemsDb;
CREATE USER ifwems_app FOR LOGIN ifwems_app;
ALTER ROLE db_datareader ADD MEMBER ifwems_app;
ALTER ROLE db_datawriter ADD MEMBER ifwems_app;
ALTER ROLE db_ddladmin ADD MEMBER ifwems_app;  -- needed once for EF migrations (both apps' migrations create their own tables/schema); can be revoked after
GO
```

You do **not** need to create the `teta` schema by hand — TETA's first `dotnet ef database
update` run (see step 6 / the deploy workflow) creates it automatically as part of its migration.

Update each app's `appsettings.Production.json` connection string (or, better, override it via a
machine-level environment variable — see step 5) with this login's password. **Never commit the
real password.** Both apps' connection strings should point at the same `IfwemsDb` database.

## 3. Create the two IIS sites, their own ports, and their app pools

Port 80 on this VPS already belongs to an unrelated, pre-existing system
(`iTrack-API`/`iTrack-UI`) — so IFWEMS and TETA each get their **own** standalone site on their
**own** port instead of sharing one site/port via sub-Applications. Pick two free ports (this
runbook uses `8083` for IFWEMS and `8082` for TETA — check `netstat -ano | findstr LISTENING`
first to confirm they're actually free on your VPS):

```powershell
Import-Module WebAdministration

# Two dedicated app pools, one per application, so they run as independent worker processes.
foreach ($pool in @("IFWEMS", "TETA")) {
    New-Item "IIS:\AppPools\$pool" -Force
    Set-ItemProperty "IIS:\AppPools\$pool" -Name "managedRuntimeVersion" -Value ""  # No Managed Code — required for ASP.NET Core Module
}

New-Item "C:\inetpub\portal\IFWEMS" -ItemType Directory -Force
New-Item "C:\inetpub\portal\TETA" -ItemType Directory -Force

New-Website -Name "IFWEMS" -PhysicalPath "C:\inetpub\portal\IFWEMS" -ApplicationPool "IFWEMS" -Port 8083
New-Website -Name "TETA" -PhysicalPath "C:\inetpub\portal\TETA" -ApplicationPool "TETA" -Port 8082

# Open the firewall for both ports (in addition to any firewall further upstream — a cloud
# provider's security group, say — which needs the same two ports opened separately).
New-NetFirewallRule -DisplayName "IFWEMS HTTP" -Direction Inbound -Protocol TCP -LocalPort 8083 -Action Allow
New-NetFirewallRule -DisplayName "TETA HTTP" -Direction Inbound -Protocol TCP -LocalPort 8082 -Action Allow
```

Leave `C:\inetpub\portal\IFWEMS` and `C:\inetpub\portal\TETA` empty for now — the first CI run (or
`deploy/deploy.ps1`) populates each one.

If IFWEMS was previously deployed directly at port 80 (an earlier version of this runbook did
that, before this VPS's port 80 was claimed by the unrelated `iTrack-*` system), move its files
into the new site's physical path and remove the old port-80 site/binding so the two don't
collide.

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
| Variable | `IFWEMS_APP_POOL_NAME` | `IFWEMS` |
| Variable | `IFWEMS_SITE_PATH` | `C:\inetpub\portal\IFWEMS` |
| Variable | `IFWEMS_HEALTH_URL` | `http://localhost:8083/health/ready` |
| Variable | `IFWEMS_SITE_URL` | `http://localhost:8083/login` |
| Variable | `TETA_APP_POOL_NAME` | `TETA` |
| Variable | `TETA_SITE_PATH` | `C:\inetpub\portal\TETA` |
| Variable | `TETA_HEALTH_URL` | `http://localhost:8082/health/ready` |
| Variable | `TETA_SITE_URL` | `http://localhost:8082/login` |

(Health checks run **on** the VPS itself, from the same GitHub Actions runner that's hosted
there — `localhost` plus each app's own port, not the public IP/path. Use whatever ports you
actually opened in step 3 if you picked different ones. `IFWEMS_SITE_URL` / `TETA_SITE_URL` are
used by the "Smoke test" steps' app-root check — they hit the SPA's own root page in a browser
sense, not just the API's `/health/ready`, since the API can be healthy while the Angular bundle
still 404s if it wasn't copied into `wwwroot` during publish. If these two variables aren't set,
that check fails with an empty/invalid URL rather than a useful error, so set them alongside the
others above.)

> If you set up this VPS before TETA existed, you previously had `IIS_SITE_NAME` / 
> `IIS_APP_POOL_NAME` / `IIS_SITE_PATH` / `SITE_HEALTH_URL` variables from the old single-app
> layout. Replace them with the `IFWEMS_*` / `TETA_*` pairs above — the workflow no longer reads
> the old names (and no longer stops/starts the parent site at all, only each app's own pool).

Also set these as **machine-level environment variables** on the VPS itself (ASP.NET Core
config automatically maps `A__B` → `A:B`), so the real secrets never live in source control
**and** so the running apps (not just the CI job) actually use them. Both apps read the same
connection string variable name; each has its **own** `Jwt__Key` (they must be different — that's
what keeps a token issued by one app from being accepted by the other):

```powershell
[Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=localhost;Database=IfwemsDb;User Id=ifwems_app;Password=<the real password>;TrustServerCertificate=True;MultipleActiveResultSets=true", "Machine")
[Environment]::SetEnvironmentVariable("Jwt__Key", "<a long random secret for IFWEMS, e.g. from `openssl rand -base64 48`>", "Machine")
```

TETA's app pool needs its **own** `Jwt__Key` (and the same `ConnectionStrings__DefaultConnection`)
set as **app-pool-specific** environment variables rather than machine-wide ones, since a
machine-wide `Jwt__Key` would apply to both pools identically. Set them on the TETA app pool
itself:

```powershell
Import-Module WebAdministration
$teta = Get-Item "IIS:\AppPools\TETA"
$teta.SetAttributeValue("environmentVariables", $null)  # ensure a clean collection
$vars = @{
    "ConnectionStrings__DefaultConnection" = "Server=localhost;Database=IfwemsDb;User Id=ifwems_app;Password=<the real password>;TrustServerCertificate=True;MultipleActiveResultSets=true"
    "Jwt__Key" = "<a different long random secret for TETA>"
    "Security__AuditSealKey" = "<a long random secret for TETA's audit log tamper-seal>"
    "Security__FieldEncryptionKey" = "<a long random secret for TETA's encrypted fields>"
}
foreach ($name in $vars.Keys) {
    $teta.environmentVariables.Add(@{ name = $name; value = $vars[$name] }) | Out-Null
}
$teta | Set-Item
iisreset
```

(The IFWEMS pool can instead use the simpler machine-wide variables above, since there's only one
app relying on that pool's environment — but setting `Jwt__Key` on the IFWEMS pool the same
per-pool way works too, and keeps both apps configured consistently. Either approach is fine as
long as the two `Jwt__Key` values are different.)

A plain app-pool recycle is **not** reliable for picking up a *machine-level* variable — WAS/W3SVC
cache the machine environment from when *they* started, not from when the registry value
changed — so after changing a machine-level variable, run `iisreset` to force it. Per-app-pool
environment variables (set via `$pool.environmentVariables`, as above) *do* take effect on the
next app-pool recycle/start, without needing a full `iisreset`.

Note the distinction: `PROD_CONNECTION_STRING` above is a **GitHub Actions secret**, used only
inside the CI job to run `dotnet ef database update` for both apps. It is separate from the
**VPS environment variables** set here, which are what the *running* IIS-hosted apps actually
read at startup — all of these need the same real connection string value, but they live in
different places and none of them automatically populates another.

Consider also creating a `production` GitHub **Environment** (Settings → Environments) with a
required reviewer, so deploys need manual approval before running on the VPS.

## 6. First deployment

Push to `main` (or run the workflow manually via **Actions → Deploy to VPS (IIS) → Run workflow**).
The workflow will, for **each** app in turn:
1. `dotnet publish` it (this transitively runs `npm ci` + `ng build --configuration production`
   for that app's own Angular client and copies the output into its `wwwroot`).
2. Apply that app's pending EF Core migrations against the shared production database.
3. Stop only that app's own app pool, mirror its publish output into its own folder, restart
   only that app's pool (the parent site and the other app's pool are never touched).
4. Hit that app's own `/health/ready` as a smoke test.

For a manual one-off deploy of a single app without CI, run on the VPS:

```powershell
./deploy/deploy.ps1 -App IFWEMS -SitePath 'C:\inetpub\portal\IFWEMS' -AppPoolName 'IFWEMS'
./deploy/deploy.ps1 -App TETA -SitePath 'C:\inetpub\portal\TETA' -AppPoolName 'TETA'
```

## 7. Later: adding a domain + HTTPS

Once a domain points at the VPS, install [win-acme](https://www.win-acme.com/) to obtain and
auto-renew a Let's Encrypt certificate bound to the **parent** IIS site (`Portal`) — a single
binding on the parent site covers both `/IFWEMS` and `/TETA` sub-paths — then set
`UseHttpsRedirection: true` in **both** apps' `appsettings.Production.json` and add an HTTPS
binding to the `Portal` site.

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

The migrations step also passes `--configuration Release` (matching the `dotnet publish` step
that runs just before it, for each app) and `--verbose`, since running `dotnet-ef` with an
implicit `Debug` configuration against a checkout whose `obj/` cache was last evaluated for
`Release` is the most concrete reproduction of this error found so far.

## Troubleshooting: smoke test fails with "HTTP Error 500.19 - Internal Server Error" (Config Error 0x8007000d)

This means IIS could not read that application's `web.config` because it doesn't recognize the
`AspNetCoreModuleV2` handler referenced in it (the `web.config` itself is auto-generated by
`dotnet publish` and is not checked into this repo, separately for each app). Almost always this
means the **.NET Hosting Bundle** (see step 1 above) is not installed on this VPS, or IIS was
never restarted after it was installed, so ASP.NET Core Module v2 was never registered with IIS —
this would affect **both** applications identically, since they share the same IIS installation.

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

The workflow has a "Verify ASP.NET Core Hosting Bundle is installed" step that checks for this up
front and fails fast with the same guidance, instead of only surfacing the problem at the
smoke-test step after the DB migrations and file copies have already run.

## Troubleshooting: `/IFWEMS/health/ready` or `/TETA/health/ready` returns "Unhealthy" (or login returns 500)

The `sqlserver` health check (and anything else that hits the database, like a login attempt)
uses `ConnectionStrings:DefaultConnection` from that app's own running configuration -- **not**
the `PROD_CONNECTION_STRING` GitHub secret. That secret only ever reaches the one-off `dotnet ef
database update` step inside the CI job; it is never passed to either IIS-hosted app process
directly. If `ConnectionStrings__DefaultConnection` was never set for that app's environment (see
step 5 above -- machine-level for IFWEMS, or per-app-pool for TETA), the running app falls back to
the placeholder committed in its `appsettings.Production.json`/`appsettings.json`
(`Password=REPLACE_ME` or the local `(localdb)` connection string), so every database call --
including the ready check and login -- fails. Since the two apps' failures are independent, one
can be healthy while the other is not; check the specific app's own environment variable.

Fix, on the VPS itself:

1. Set the connection string for the affected app (machine-level env var + `iisreset` for
   IFWEMS, or the app-pool environment variable + pool recycle for TETA — see step 5).
2. Re-check that app's `/health/ready` URL; it should now report healthy.

If it's still unhealthy after that, confirm the value itself is correct (matches the same
connection string that was tested successfully with `sqlcmd`/`Test-NetConnection`, including
`TrustServerCertificate=True`), and that the `ifwems_app` SQL login has access to `IfwemsDb`.
