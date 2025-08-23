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
- If you ever forget the password, the site administrator can clear the saved password value in the database to enable the “set a new password” screen again.

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
