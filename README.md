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

### Password hashing details (early auth milestone)
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
The API persists data to a local SQLite file at `App_Data/receptregister.db` (created on first run). Schema is simple:
- Recipes (Name, Book, Page, Notes, Tried)
- Categories & Keywords (unique name each, stored lowercase)
- Join tables (`RecipeCategories`, `RecipeKeywords`) for many-to-many links

Foreign keys are enforced, and removing a recipe cascades its join rows. Category / keyword master rows remain (so taxonomy grows as you add terms). Back up is as easy as copying the single `.db` file while the app is stopped.

In future milestones this may evolve (migrations, encryption, cloud backup), but for now the priority is a small, dependency-light foundation you can understand at a glance.

— “Let’s sift the chaos and find the perfect recipe to bake today.” — Bagare Bengtsson

## Running locally (Milestone 1 scaffolding)

Two apps make up ReceptRegister:
- API (Minimal API): hosts the JSON endpoints and persistence
- Frontend (Razor Pages): serves the HTML UI and static assets

### Option 1: Quick start (two terminals)
```powershell
dotnet watch run --project ReceptRegister.Api
```
```powershell
dotnet watch run --project ReceptRegister.Frontend
```
Then browse the frontend (it calls into the API). Health checks:
- API:   GET https://localhost:<api-port>/health -> JSON `{ "status": "ok" }`
- Front: GET https://localhost:<frontend-port>/health -> plain text `ok`

### Option 2: Orchestration script
Use the helper script which launches both with file watching:
```powershell
./run-dev.ps1
```
Press Enter in the script window to stop both processes.

### Ports
Default Kestrel development ports are assigned by ASP.NET; you can pin them in each project Properties/launchSettings.json if you prefer stable values.

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

## Dependency policy (early milestones)

To keep the code understandable and portable:
- No external CSS/JS frameworks (no Bootstrap, Tailwind, etc.)
- No client-side bundler; ES modules loaded directly
- Minimal NuGet dependencies; prefer platform features first

This constraint can be revisited in later milestones if/when complexity warrants it.

— “Let’s sift the chaos and find the perfect recipe to bake today.” — Bagare Bengtsson
