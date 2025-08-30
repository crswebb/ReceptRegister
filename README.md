# ReceptRegister

ReceptRegister is your personal, searchable index for pastry recipes from your book collection. Instead of flipping through sticky notes and indexes, you can find the right recipe in seconds and jump straight to the page.

## What you can store
- Recipe name (e.g., “Kanelbullar”)
- Book title (which book it comes from)
- Page number (where to find it in the book)
- Categories (one or more, like “Buns”, “Cookies”, “Swedish”)
- Keywords (one or more, like “cardamom”, “chocolate”, “gluten-free”)
- Tried checkbox (mark whether you’ve baked it yet)

## What you can do
- Search by name, book, category, or keyword.
- Quickly see the exact page number in the right book.
- Filter by “tried” or “not tried” to plan your next bake.
- Browse by book or category when you’re in the mood for a certain style.
- Update entries as you explore your library.
 - (API) Query with paging & combined filters (book, category ids, keyword ids, tried) for efficient large libraries.

## How it feels to use
- A simple search bar to find recipes by words you remember.
- Clear filters for book, category, and tried status.
- A tidy list showing: Name • Book • Page • Categories • Tried.
- A focused details view to review and edit a recipe’s information.

## Everyday examples
- Type “cardamom” to find every recipe with that flavor.
- Filter by “Buns” to plan a fika spread.
- Look up “Bröd och Bageri” and jump to page 123.
- Show only “not tried” recipes to pick your next bake.

## Why it’s helpful
Your shelves stay beautiful, your pages stay clean, and your baking time goes into actual baking—not searching. Think of it as the well‑labeled spice rack for your recipe books.

## Security and access
- The app is protected with a password.
- On first visit, if no password has been set yet, you’ll be guided to create one.
- After that, you’ll sign in before you can use the app.
- If you ever forget the password, the site administrator can clear the saved password value in the database to enable the “set a new password” screen again (see [Manual recovery](#manual-recovery-quick-steps)).

### Password hashing & password strength
Passwords are hashed with PBKDF2 (SHA‑256) using:

- A per‑user random 32‑byte salt
- Configurable iteration count (env `RECEPT_PBKDF2_ITERATIONS`, default 150,000)
- Optional secret application “pepper” appended to the password before hashing (env `RECEPT_PEPPER`)

Environment variables:

| Variable | Purpose | Recommendation |
|----------|---------|----------------|
| `RECEPT_PBKDF2_ITERATIONS` | Override default iterations | Keep >= 150k; raise gradually over time |
| `RECEPT_PEPPER` | Global secret pepper to add defense if DB is leaked | Set to a long random string in production; leave unset locally |

If no pepper is configured a warning is logged at startup (safe for local dev). Changing the pepper after setting a password will invalidate verification (you would need to reset the password). Keep it stable and rotate only with a coordinated password reset.

Password strength is evaluated server‑side (source of truth) with a 0–6 score (length >=8, length >=12, lowercase, uppercase, digit, symbol). A score of 3+ is required. The client progressively enhances the Set Password UI with a small meter and top suggestions; validation still occurs on the server.

### Sessions & authentication endpoints
After setting a password via `POST /auth/set-password`, obtain a session with `POST /auth/login`.

Responses:
- `POST /auth/login` => `{ "expiresAt": "2025-08-23T12:34:56Z", "csrf": "<token>" }` and sets an `rr_session` HttpOnly cookie.
- `GET /auth/status` => `{ hasPassword, authenticated, expiresAt?, csrf? }` (csrf & expiry present only when authenticated).

Include the `X-CSRF-TOKEN` header with the value returned from status/login for any state‑changing method (POST/PUT/PATCH/DELETE). Read‑only GET endpoints do not require it.

Password management endpoints:
- `POST /auth/set-password` (one-time initial)
- `POST /auth/change-password` (requires current password + CSRF)
- `POST /auth/refresh` (extends current session lifetime; requires existing valid session)

Rate limiting:
- Login attempts are limited (env controlled; default 5 attempts per 5 minutes per IP). Exceeding returns HTTP 429.

Environment variables (additional):

| Variable | Purpose | Recommendation |
|----------|---------|----------------|
| `RECEPT_SESSION_MINUTES` | Session lifetime in minutes | 120 default; adjust to usage pattern |
| `RECEPT_LOGIN_MAX_ATTEMPTS` | Max failed logins per window | 5 (raise carefully) |
| `RECEPT_LOGIN_WINDOW_SECONDS` | Sliding window size | 300 |

The built‑in session store is in‑memory (single process). If you redeploy or restart, sessions are invalidated. Future milestone can add persistent or distributed storage.

### Password recovery & rotation
If the admin password is lost you can force the application back into the initial "set password" state.

1. Stop the API process.
2. Run the following SQL against the SQLite database file (`App_Data/receptregister.db`):

```
DELETE FROM AuthConfig WHERE Id=1;
VACUUM; -- optional, reclaims some space
```

3. Start the API, then call `GET /auth/status` – `hasPassword` will be `false` and you can `POST /auth/set-password` again.

Pepper rotation (changing `RECEPT_PEPPER`):
- Changing the pepper invalidates every existing password hash (verification will fail) because the pepper is part of the derived key input.
- Recommended procedure:
	1. Schedule maintenance (read‑only window).
	2. Delete the existing row as above (forces re‑set).
	3. Set the new strong pepper secret in the environment.
	4. Start API and set a new password.
- Avoid changing the pepper without resetting the stored hash; users would just be locked out.

Iteration increases (`RECEPT_PBKDF2_ITERATIONS`):
- Safe to raise at any time; existing hashes upgrade lazily on the next successful login (transparent rehash) so no bulk migration needed.
- Lowering iterations is discouraged; if you must (e.g., temporary performance constraint) it will only apply to new / upgraded hashes.

Disaster recovery quick checklist:
- Backup: copy `receptregister.db` while the process is stopped.
- Restore: replace the file with a backed‑up copy, ensure file permissions allow the app to read/write.
- Confirm integrity: run a simple query (`SELECT COUNT(*) FROM Recipes;`).

Security reminders:
- Keep `RECEPT_PEPPER` out of source control (environment / secret store only).
- Rotate the pepper only with a planned password reset.
- Raise iterations opportunistically (track approximate hash time; target <250ms on your hardware).

### Manual recovery (quick steps)

If the admin password is lost and you just want to get back in:

1. Stop the application process.
2. Make a backup copy of `App_Data/receptregister.db` (copy the file somewhere safe).
3. Remove the password row using any SQLite tool or CLI:
	```sql
	DELETE FROM AuthConfig WHERE Id=1;
	```
	(Optional) run `VACUUM;` after to reclaim space.
4. Start the application again.
5. Visit the site (or call `GET /auth/status`) – it will show that no password is set, allowing you to set a new one.

Alternative: you may delete the entire `receptregister.db` file instead (after backing it up), but this erases all stored recipes—only do that if you intend to start fresh.

Keep the backup until you confirm the new password works and data (if preserved) is intact.

## Future ideas
- Import from a simple spreadsheet to add many recipes at once.
- Mark favorites or add a quick rating.
- Add personal notes and tips you discover while baking.
- Attach photos of results for inspiration.
- Print or share a shortlist when planning a baking day.

## Data storage (early alpha)
The API persists data to a local SQLite file at `App_Data/receptregister.db` (created on first run) by default. (NEW: An experimental SQL Server provider is now selectable – see next section.) Schema is simple:
- Recipes (Name, Book, Page, Notes, Tried)
- Categories & Keywords (unique name each, stored lowercase)
- Join tables (`RecipeCategories`, `RecipeKeywords`) for many-to-many links

Foreign keys are enforced, and removing a recipe cascades its join rows. Category / keyword master rows remain (so taxonomy grows as you add terms). Back up is as easy as copying the single `.db` file while the app is stopped.

In future milestones this may evolve (migrations, encryption, cloud backup), but for now the priority is a small, dependency-light foundation you can understand at a glance.

### Database provider selection (experimental)

You can choose between the built-in file-based SQLite store (default) and SQL Server (including Azure SQL). Configuration keys (in `appsettings.json` or environment):

```json
{
	"Database": {
		"Provider": "SQLite", // or "SqlServer"
		"ConnectionString": "" // required only when Provider = SqlServer
	}
}
```

Notes:
- If `Database:Provider` is omitted or blank, SQLite is used.
- When `Provider=SqlServer`, `Database:ConnectionString` must be supplied; the application will fail fast at startup if missing.
- Current schema & repositories still use SQLite-specific SQL (e.g. `last_insert_rowid()`, `INSERT OR IGNORE`). SQL Server support is being added incrementally in issues #105–#107.
- A sample file `ReceptRegister.Api/appsettings.Database.sample.json` is included (copy/merge into your real settings file without comments if you need a template).

Progress (Epic #110):
1. Provider-specific schema initialization (#106) – DONE: a provider abstraction (`ISchemaInitializer`) now creates either the SQLite or SQL Server schema at startup.
2. Neutral SQL for repositories / dialect adjustments (#107) – DONE (initial pass): repositories now use a runtime `IDatabaseDialect` (SQLite or SQL Server) for inserts, identity retrieval, taxonomy upserts, link creation, and paging. Further optimization & consolidation may follow.
3. Data migration helper (#108) – INITIAL IMPLEMENTATION: one-shot SQLite -> SQL Server migrator (see Migration section below).
4. Documentation expansion (#109) – PENDING.

Implementation notes (#106 / #107):
- A new `ISchemaInitializer` is registered based on `Database:Provider` and invoked during startup (API & Frontend host).
- `SqliteSchemaInitializer` contains the former static DDL logic (transactional create-if-not-exists).
- `SqlServerSchemaInitializer` currently provisions an equivalent schema (tables, PKs, FKs, indexes) using `IF NOT EXISTS` guards; still experimental.
- Razor Pages taxonomy add/delete handlers now also use the dialect abstraction for taxonomy upsert (aligned with repositories).

Current limitation: No automated integration test yet verifies SQL Server end-to-end (planned alongside #108 migration tooling).
### Data migration (SQLite to SQL Server)

An initial migration helper now exists to move data from an existing SQLite `receptregister.db` file into a SQL Server database.

Usage (target = SQL Server):

1. Configure `Database:Provider=SqlServer` and a valid `Database:ConnectionString` (env vars or appsettings).
2. Run the API project with the `--migrate-sqlite=<path-to-existing-sqlite-db>` argument.
	Example PowerShell:
	```powershell
	dotnet run --project ReceptRegister.Api -- --migrate-sqlite="C:\\data\\receptregister.db" 
	```
3. The process will:
	- Ensure the target schema exists (runs `ISchemaInitializer`).
	- Read all taxonomy + recipes from the SQLite file (read-only).
	- Upsert taxonomy terms (categories / keywords) into SQL Server (skips existing names).
	- Insert recipes skipping any that already exist by natural key (Name + Book + Page).
	- Recreate many-to-many links.
4. On success it prints a summary and exits without starting the web server.

Safety / idempotency:
- You can re-run; existing taxonomy terms are ignored and duplicate recipes (natural key) are skipped.
- Migration never deletes data in the target.

Limitations / future enhancements:
- Only supports SQLite -> SQL Server (not reverse, not incremental diffs).
- Does not yet migrate the `AuthConfig` password row (intentional; set a new password after migrating content).
- No batching / streaming for extremely large datasets (loads all recipe rows into memory first — acceptable for small personal libraries).
- No automated integration test yet exercises the migrator (planned). 

After migration:
- Start the combined Frontend (or API) normally pointing at SQL Server.
- Visit the site; you will be in setup mode (no password) unless you manually migrated AuthConfig which is intentionally not handled.
- Set a new password and verify recipes appear.

### Azure SQL quickstart (experimental provider)

You can point the application at an Azure SQL Database (recommended for a small personal deployment: serverless, basic tier). These steps assume you already have an Azure subscription.

1. Create resource (Portal):
	- Search "Azure SQL" -> "Create" -> "SQL database".
	- Select (or create) a logical server. For low-cost dev, choose serverless compute if available; basic tiers are also fine (recipe index is small).
	- Enable "Allow Azure services and resources to access this server" during creation (simplifies firewall).
2. Networking / firewall:
	- Add your current client IP so you can connect initially. (Or later, configure a private endpoint / specific IP range.)
3. Authentication:
	- Use SQL auth with a strong admin password (store it in a password manager). Azure AD auth is possible but not documented here.
4. Connection string:
	- After provisioning, go to the database -> "Connection strings" -> copy the ADO.NET connection string. It looks like: `Server=tcp:<servername>.database.windows.net,1433;Initial Catalog=<dbname>;Persist Security Info=False;User ID=<user>;Password=<pw>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`
	- DO NOT commit this; store in environment variable or user secrets.
5. App configuration:
	- Set `Database:Provider=SqlServer`.
	- Set `Database:ConnectionString` to the copied string (ideally via environment variable, e.g. `RECEPTREGISTER__Database__ConnectionString`).
6. First run:
	- Start the application; schema will auto-create. Confirm logs show `SqlServerSchemaInitializer` ran.
7. (Optional) Migrate existing local SQLite data:
	- Run migration mode: `dotnet run --project ReceptRegister.Api -- --migrate-sqlite="C:\\path\\to\\receptregister.db"` then start normally.

Cost & performance notes:
- Serverless Azure SQL can pause; expect a cold-start latency (several seconds) on first query after idle period.
- For extremely low usage you might still prefer file-based SQLite to avoid hosting cost entirely.
- The application issues light, parameterized queries; the Basic / S0 tier should be ample.

Security reminders:
- Always use `Encrypt=True;TrustServerCertificate=False` (default from portal) to enforce TLS.
- Restrict firewall to only the hosts that need access (App Service, your IP, etc.).
- Never commit the connection string; prefer environment injection / Azure configuration.

### Azure SQL troubleshooting

| Symptom | Cause | Suggested fix |
|---------|-------|---------------|
| `Login failed for user` | Wrong username or password | Regenerate password in portal; update env variable |
| `Cannot open server requested by the login` | Wrong server or database name | Verify `Server=` and `Initial Catalog=` values |
| `Client with IP address ... is not allowed to access the server` | Firewall rule missing | Add client IP in server Networking settings |
| Long first query (~5-15s) | Serverless database resumed | Expected; subsequent queries faster |
| `Certificate chain was issued by an authority that is not trusted` (local dev) | Older dev machine certificate store | Keep `Encrypt=True;TrustServerCertificate=False`; ensure OS root certs updated (Windows Update). Avoid disabling encryption |
| Migration tool very slow | High network latency + many round trips | (Future) Batch insert enhancement (#118/#120); for now run from a machine in same region |

## Release Branch & Azure App Service Deployment

A GitHub Actions workflow (`.github/workflows/release-deploy.yml`) builds + deploys the unified Frontend (which hosts the API) when you push to any branch matching `release/**`.

### Setup Steps
1. Create a release branch, e.g. `release/2025-09-initial`.
2. In Azure: provision an App Service (Linux) targeting .NET 8+.
3. Configure an Azure AD federated identity (recommended) or create a service principal.
4. Add these repository secrets:
   - `AZURE_CLIENT_ID` – App registration (service principal) client ID.
   - `AZURE_TENANT_ID` – Directory (tenant) ID.
   - `AZURE_SUBSCRIPTION_ID` – Subscription containing the Web App.
   - `AZURE_WEBAPP_NAME` – Name of the Web App resource.
   - `AZURE_WEBAPP_HOST` – Host name (e.g. `myrecept.azurewebsites.net`).
5. Add application settings in App Service (Configuration > Application settings):
   - `RECEPT_PBKDF2_ITERATIONS` (e.g. 180000)
   - `RECEPT_PEPPER` (strong secret – DO NOT COMMIT)
   - `RECEPTREGISTER__Database__Provider=SqlServer`
   - `RECEPTREGISTER__Database__ConnectionString=Server=...;Initial Catalog=...;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=False;`
6. Push commits to the `release/...` branch; workflow will build & deploy.

Manual run: Use the workflow_dispatch event (can toggle deploy boolean). Health endpoint is probed post-deploy (`/health`).

Rollback: push a revert commit to the same release branch (or create a new `release/...` branch at previous good commit). Consider adding deployment slots later for zero-downtime swaps.

---

— “Let’s sift the chaos and find the perfect recipe to bake today.” — Bagare Bengtsson

## Documentation & maintenance scripts

Additional security notes live in `SECURITY.md` (session/CSRF design, environment variables, threat model). A helper script `tools/prune-merged.ps1` can archive (tag) and delete fully merged feature branches; read its synopsis (`Get-Content tools/prune-merged.ps1 | more`) before use. Always review tags pushed to ensure no unreviewed work is lost.

## Running locally (Milestone 1 scaffolding)
## API (Milestone 4)

### Core Endpoints

Recipes:
- GET /recipes?query=&book=&categoryId=1&categoryId=2&keywordId=3&tried=true&page=1&pageSize=20
	Returns: `{
		"items": [ { id, name, book, page, tried, categories[], keywords[] } ],
		"page": 1, "pageSize": 20, "totalItems": 57, "totalPages": 3
	}`
- GET /recipes/{id}
- POST /recipes (RecipeRequest)
- PUT /recipes/{id}
- POST /recipes/{id}/tried { id, tried }
- DELETE /recipes/{id}
- POST /recipes/{id}/categories/{categoryId}
- DELETE /recipes/{id}/categories/{categoryId}
- POST /recipes/{id}/keywords/{keywordId}
- DELETE /recipes/{id}/keywords/{keywordId}

Taxonomy:
- GET /categories (list names)
- GET /keywords (list names)

### Query Parameters
- query: free text across name/book/notes/categories/keywords
- book: exact match on stored book title
- categoryId / keywordId: repeatable; recipe must match ANY of supplied ids for each dimension
- tried: true|false
- page / pageSize: paging (defaults 1 / 20, max pageSize 100)

### Validation & Errors
Errors follow Problem Details (RFC 9457) with custom types:
- Validation: type=https://receptregister/errors/validation (422)
- Not found: type=https://receptregister/errors/not-found (404)
- Conflict: type=https://receptregister/errors/conflict (409)

Example 404:
```json
{
	"type": "https://receptregister/errors/not-found",
	"title": "Resource not found",
	"status": 404,
	"detail": "Recipe 42 not found"
}
```

### Tried Endpoint Change
Legacy PATCH /recipes/{id}/tried replaced by POST /recipes/{id}/tried with body `{ id, tried }`.

---

Two apps make up ReceptRegister:
- API (Minimal API): hosts the JSON endpoints and persistence
- Frontend (Razor Pages): serves the HTML UI and static assets

### Option 1: Single process (recommended now)
The frontend project hosts both the UI pages and the API endpoints (same origin):
```powershell
dotnet watch run --project ReceptRegister.Frontend
```
Health check: `GET https://localhost:<frontend-port>/health` -> `ok` (also JSON under `/api/health` if defined).

### Option 2: Legacy two‑process (if you prefer separate)
You can still run the API alone (for tests or experimentation) and point the frontend meta `api-base` to it:
```powershell
dotnet watch run --project ReceptRegister.Api
dotnet watch run --project ReceptRegister.Frontend
```
Adjust the `<meta name="api-base" />` tag if using a fixed API port.

### Option 3: Orchestration script (if retained)
If `run-dev.ps1` exists you can continue to use it to launch both; otherwise single process is simplest.

### Ports
Default Kestrel development ports are assigned by ASP.NET; you can pin them in each project Properties/launchSettings.json if you prefer stable values.

### HTTPS in development
The application only enables `UseHttpsRedirection()` outside of `Development`. Rationale:
- Keeps local startup logs clean (avoids "Failed to determine the https port" warning when only HTTP is configured).
- Simplifies first-run experience (no dev certificate prompts).
- Session cookies are still marked HttpOnly; for production deployment you should run behind HTTPS (reverse proxy or Kestrel) so HSTS + redirection apply.

If you want HTTPS locally:
1. Trust/create a dev cert: `dotnet dev-certs https --trust`
2. Run the HTTPS profile: `dotnet run --project ReceptRegister.Frontend --launch-profile https`
3. Optionally move the `app.UseHttpsRedirection()` call back outside the environment check or add an explicit HTTPS Kestrel endpoint in `appsettings.Development.json`.

## Publishing (self-contained example)

Build a self‑contained release for Windows x64 (adjust RID as needed):
```powershell
dotnet publish ReceptRegister.Api -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
dotnet publish ReceptRegister.Frontend -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```
The output folders will be under each project's `bin/Release/<tfm>/<rid>/publish/`.

Environment variables (pepper, iteration count, etc.) should be supplied via your host OS or service configuration. The SQLite database file is created alongside the API (see `App_Data`). Back it up by copying the single `.db` file while the API is stopped.

## Folder conventions

Frontend static asset layout:
- `wwwroot/css/` : Base styles (`base.css`, site-wide styles in `site.css`)
- `wwwroot/js/` : General scripts; `modules/` contains ES modules (progressive enhancement)
- `wwwroot/js/modules/placeholder.js` : Intentional no-op scaffold so import patterns are established early

### Theming & design tokens (UI polish milestone)

A lightweight token system (no framework) powers theming and consistency.

Tokens live in `wwwroot/css/variables.css`:
- Colors: `--color-*` (background, surface, text, primary, danger, success, warning, link, focus, shadows, accent backgrounds)
- Spacing scale: `--spacing-0..8` (2px–32px) used instead of magic numbers
- Typography: font sizes (`--fs-*`), line heights, weights (`--fw-*`)
- Radii: `--radius-*` (including `--radius-pill` for chips)
- Elevation: shadow presets `--elevation-*`
- Transitions, z-index layers

Dark mode: automatic via `prefers-color-scheme: dark` with explicit override using a theme toggle button (writes `data-theme="dark|light"` on `<html>` and persists preference in `localStorage` key `rr_theme`).

Custom components refactored to use tokens:
- Buttons (.btn variants: default primary, outline, subtle, danger, secondary; sizes sm/lg)
- Forms (consistent spacing, hint + error states, accessible focus rings)
- Tables (responsive stacking on narrow viewports; visually hidden caption for SR users)
- Pagination (button styling alignment)
- Recipe list can toggle between table view and new card grid layout (persisted preference `rr_recipe_layout`).

Adding a new component: use existing variables; if a new semantic color is needed prefer deriving from an existing palette value and add a well‑named token (e.g. `--color-info`). Avoid embedding raw hex values in component files.

Accessibility & contrast: color selections meet WCAG AA for text (normal 4.5:1, large 3:1). Focus indicators use `--color-focus-outline` with a 2px outline for visibility across themes.

### Localization (fixed culture groundwork)

Localization groundwork is in place so UI strings can be translated. Currently the application uses a fixed culture configured in `appsettings.json` (no end‑user language switcher yet). See Issues: #97 (overall), #99 (extraction), #100 (Swedish), #98 (this documentation).

#### Configuration

Each project can specify a `Localization` section:

```jsonc
"Localization": {
	"DefaultCulture": "en-US",
	"SupportedCultures": [ "en-US" ]
}
```

At startup localization middleware sets the thread cultures to the configured default. Only a fixed provider is registered (no query string, cookie, or Accept-Language negotiation) for deterministic behavior.

Change the global culture (example Swedish):
1. Edit `ReceptRegister.Api/appsettings.json` and/or `ReceptRegister.Frontend/appsettings.json`:
   ```jsonc
   "Localization": { "DefaultCulture": "sv-SE", "SupportedCultures": [ "sv-SE" ] }
2. Restart the application.

> Adding multiple codes to `SupportedCultures` today has no visible effect because user selection is not exposed yet; the default still applies.

#### Resource files

UI strings reside in `ReceptRegister.Frontend/Resources/`:
- `SharedResources.resx` (neutral/default English)
- `SharedResources.sv.resx` (placeholder Swedish values – translation tracked in #100)

Marker class: `SharedResources` (for `IStringLocalizer<SharedResources>` injection).

Key prefixes:
- `Nav.*` navigation
- `Button.*` buttons/actions
- `Table.*` column headers
- `Page.*` titles / empty states
- `Form.*` labels & hints
- `A11y.*` accessibility & live region text

#### Adding a new UI string
1. Choose prefix, add key to `SharedResources.resx`.
2. Add the same key to every other culture file (even if untranslated initially) to avoid silent fallback.
3. In Razor: `@inject IStringLocalizer<SharedResources> L` then `@L["Button.Search"]`.

#### Dynamic strings
Live region announcements use format placeholders (`{0}`, `{1}`) to allow language-specific grammar. Preserve placeholders exactly when translating.

Example:
```csharp
@string.Format(L["A11y.Recipes.ResultsCount"], total, pluralSuffix, page, totalPages)
```

#### Translation workflow (planned)
1. Extract strings (done, #99).
2. Provide Swedish translations (#100).
3. Add optional user switcher & negotiation enhancements (#97 future phase).

#### Troubleshooting
| Symptom | Cause | Fix |
|---------|-------|-----|
| English still displays after culture change | App not restarted / only one project changed | Restart; update both configs |
| Placeholder `{0}` shows literally | Missing `string.Format` | Wrap the resource usage with formatting |
| Some items revert to English | Key missing in target culture file | Add key to that culture file |

#### Adding another language now
1. Copy `SharedResources.resx` to `SharedResources.<culture>.resx` (e.g. `fr-FR`).
2. Translate values (keep placeholders).
3. Add culture code to `SupportedCultures` (and optionally set as `DefaultCulture`).
4. Restart.

Future enhancement: user-selectable language + persisted preference (tracked in #97).

## Dependency policy (early milestones)

To keep the code understandable and portable:
- No external CSS/JS frameworks (no Bootstrap, Tailwind, etc.)
- No client-side bundler; ES modules loaded directly
- Minimal NuGet dependencies; prefer platform features first

This constraint can be revisited in later milestones if/when complexity warrants it.

— “Let’s sift the chaos and find the perfect recipe to bake today.” — Bagare Bengtsson
