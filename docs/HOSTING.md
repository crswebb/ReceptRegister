# Hosting ReceptRegister

This guide collects practical options for running ReceptRegister beyond local `dotnet watch` development. Pick the path that matches your scale + comfort. The app is small, CPU‑light, and low traffic; optimize for simplicity first.

## TL;DR Matrix

| Scenario | Recommended Option | Data Store | When to choose |
|----------|--------------------|-----------|----------------|
| Personal on a home PC / NAS | Windows/Linux service (self‑contained publish) | SQLite file | Easiest – you control the box |
| Low‑cost always‑on cloud | Azure Container Apps (ACA) or Azure App Service | SQLite (container volume) or Azure SQL | Minimal ops; managed SSL |
| Sporadic hobby usage, near $0 cost | On‑demand run (App Service Free / Azure Container Apps scale to zero) | SQLite (ephemeral) or Azure SQL serverless | Accept cold starts |
| Desire for one-file portable deploy | Single-file self‑contained binary | SQLite | Copy & run anywhere |
| Future multi‑instance scaling | Container (Docker) + external Azure SQL | Azure SQL | Required for horizontal scale |

## 1. Build Artifacts

Create release builds:

```powershell
# Frontend hosts API (single process deployment)
dotnet publish ReceptRegister.Frontend -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
# (Optional) Linux artifact
 dotnet publish ReceptRegister.Frontend -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```
Publish output: `ReceptRegister.Frontend/bin/Release/<tfm>/<rid>/publish/`

Set environment variables (pepper, iterations, database provider) in the host – NOT in `appsettings.json` checked into version control.

## 2. Configuration (Production Checklist)

Environment variables (examples PowerShell):
```powershell
$env:RECEPT_PBKDF2_ITERATIONS = 180000
$env:RECEPT_PEPPER = '<long-random-secret>'
$env:RECEPTREGISTER__Database__Provider = 'SqlServer'   # or SQLite
$env:RECEPTREGISTER__Database__ConnectionString = 'Server=...;Initial Catalog=...;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=False;'
```

Additional hardening suggestions:
- Run behind HTTPS / reverse proxy (Nginx, Caddy, IIS, App Service built‑in).
- Restrict inbound firewall to 80/443 (or just 443).
- Keep OS patched.
- Back up the SQLite file (if using SQLite) or enable Azure SQL automated backups (default).

## 3. Option A – Windows Service (self host)

1. Publish self‑contained `win-x64` (above).
2. Copy publish folder (or just the single file) to target directory, e.g. `C:\Apps\ReceptRegister`.
3. Create a service (PowerShell, requires Administrator):
```powershell
New-Service -Name ReceptRegister -BinaryPathName 'C:\Apps\ReceptRegister\ReceptRegister.Frontend.exe' -Description 'ReceptRegister unified app' -StartupType Automatic
Start-Service ReceptRegister
```
4. Set environment variables system‑wide (System Properties > Environment) or via registry for the service account.
5. (Optional) Put a reverse proxy (IIS/ARR or Nginx on WSL) for HTTPS termination. Or generate a Kestrel cert & configure `ASPNETCORE_URLS=https://+:443;http://+:80`.
6. Test health: `curl http://servername/health` -> `ok`.

Upgrade: `Stop-Service ReceptRegister`, replace files, `Start-Service ReceptRegister`.

## 4. Option B – Linux systemd

1. Publish `linux-x64` artifact.
2. Copy to `/opt/receptregister` and mark executable: `chmod +x ReceptRegister.Frontend`.
3. Create `/etc/systemd/system/receptregister.service`:
```
[Unit]
Description=ReceptRegister unified app
After=network.target

[Service]
WorkingDirectory=/opt/receptregister
ExecStart=/opt/receptregister/ReceptRegister.Frontend
Environment=RECEPT_PBKDF2_ITERATIONS=180000
Environment=RECEPT_PEPPER=__REDACTED__
Environment=RECEPTREGISTER__Database__Provider=SQLite
#Environment=RECEPTREGISTER__Database__Provider=SqlServer
#Environment=RECEPTREGISTER__Database__ConnectionString=Server=...;Initial Catalog=...;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=False;
Restart=always
User=www-data

[Install]
WantedBy=multi-user.target
```
4. Enable + start: `systemctl enable --now receptregister`.
5. Reverse proxy (Nginx/Caddy) for HTTPS & static caching (optional; app can serve directly).

## 5. Option C – Docker Container

Add a `Dockerfile` (multi-stage):
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish ReceptRegister.Frontend -c Release -o /app/publish

# Runtime stage (ASP.NET runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
# Environment placeholders (override at deploy time)
# ENV RECEPT_PEPPER=... RECEPTREGISTER__Database__Provider=SQLite
EXPOSE 8080
ENTRYPOINT ["dotnet", "ReceptRegister.Frontend.dll"]
```
Build & run locally:
```powershell
docker build -t receptregister .
docker run -p 8080:8080 -e RECEPT_PEPPER=secret receptregister
```
SQLite persistence:
```powershell
# Mount volume so DB file survives container restarts
docker run -p 8080:8080 -v recept_data:/app/App_Data -e RECEPT_PEPPER=secret receptregister
```
Switch to Azure SQL:
```powershell
docker run -p 8080:8080 -e RECEPT_PEPPER=secret -e RECEPTREGISTER__Database__Provider=SqlServer -e RECEPTREGISTER__Database__ConnectionString='Server=...;...' receptregister
```

### Container + Migration
To migrate existing local SQLite data into a fresh Azure SQL:
1. Run a one-shot container with migration arg:
```powershell
docker run --rm -e RECEPTREGISTER__Database__Provider=SqlServer -e RECEPTREGISTER__Database__ConnectionString='Server=...;' -v C:\path\to\localdb:/import receptregister dotnet ReceptRegister.Api.dll --migrate-sqlite="/import/receptregister.db"
```
2. Then run the normal frontend container pointing at SQL Server.

## 6. Option D – Azure App Service

Simplest (no container) using build from source:
1. Create App Service (Linux plan) + enable .NET 8/10.
2. Deploy via `az webapp up` or GitHub Action.
3. Configure settings (App Service > Configuration) for environment variables.
4. (Optional) Use deployment slot for staging.
5. Enable "Always On" if using Azure SQL to keep cold starts low.

SQLite on App Service is NOT recommended for durability (filesystem can recycle). Prefer Azure SQL when on App Service.

## 7. Option E – Azure Container Apps

1. Build & push image to ACR or GHCR.
2. Create Container App with ingress (HTTP) and min replicas = 0 or 1.
3. Set secrets (pepper, connection string) as environment variables / secrets.
4. If using SQLite, add a volume (Azure File share) and mount to `/app/App_Data`.

Scale to zero: requests incur cold start; fine for personal use.

## 8. Backups & Disaster Recovery

SQLite:
- Stop process (or ensure quiescent) then copy `App_Data/receptregister.db`.
- Automate with a daily scheduled task / cron copying to cloud storage.

Azure SQL:
- Built-in PITR backups (7–35 days). For long-term keep, configure Long-Term Retention (LTR).
- Optional: export a bacpac (`sqlpackage`) periodically for offline copy.

Test restores quarterly (practice makes real recovery faster).

## 9. Observability

Current minimal:
- Health endpoint `/health` (string `ok`) and underlying dependency checks (extensible via `AddAppHealth`).
- Application logs (console). Capture to host log system (journalctl, App Service log stream, Container Logs).

Enhancements you can add later:
- Structured logging (Serilog sink) -> blob / seq / application insights.
- Basic metrics: wrap repositories with timing to log duration percentiles.
- Uptime probe (external) hitting `/health` + simple recipe query.

## 10. Security Hardening

- Enforce HTTPS: terminate TLS at proxy (Nginx, App Service, ACA) or configure Kestrel cert.
- Set `Cookie.SecurePolicy = Always` (future improvement) when assured of HTTPS.
- Add reverse proxy headers middleware if behind proxy (for accurate scheme) – currently relying on default; consider `app.UseForwardedHeaders()` when deploying behind Nginx/ACA.
- Store pepper in secret store: (App Service secrets, ACA secrets, Azure Key Vault ref, systemd environment file with restricted perms).
- Regularly raise PBKDF2 iterations if login latency acceptable (<250ms hash time).
- Restrict SQL Server firewall to only deployment region + management IP.

## 11. Multi-Instance Scaling Considerations

Today: in-memory session store => sticky sessions required if you scale out (or all logins invalidated on instance swap).

If you NEED scale-out soon:
- Introduce a distributed cache (e.g. Redis) and implement an `ISessionStore` (future milestone note).
- Move static files to a CDN for lower latency (optional; site is very small now).

## 12. Upgrade / Rollback Strategy

Simple approach:
1. Back up DB (SQLite file copy / verify Azure SQL backup retention).
2. Deploy new version alongside old (container tag `:vNext`).
3. Smoke test `/health` + login + recipe list.
4. Switch traffic (update container revision / swap slot / service restart).
5. If failure, revert to previous image or binary; database schema currently stable (no destructive migrations yet).

## 13. Checklist (Initial Production Go-Live)

- [ ] Decide data provider (SQLite vs SQL Server/Azure SQL)
- [ ] Generate strong pepper & store securely
- [ ] Set PBKDF2 iterations >= 150k
- [ ] Publish & deploy unified frontend host
- [ ] Enforce HTTPS
- [ ] Configure backup (file copy or rely on Azure SQL PITR)
- [ ] Verify `/health` and password setup
- [ ] Run a search + details view manually
- [ ] Document admin recovery steps location

---
Feel free to request an automation script (Dockerfile addition, GitHub Action workflow, or Azure provisioning template) and this doc can be expanded further.
