# Localization

Centralized details on culture configuration, resource files, and translation workflow.

## Contents
- [Runtime Overrides](#runtime-overrides)
- [Fixed Culture Configuration](#fixed-culture-configuration)
- [Resource Files](#resource-files)
- [Adding UI Strings](#adding-ui-strings)
- [Dynamic Strings](#dynamic-strings)
- [Translation Workflow](#translation-workflow)
- [Troubleshooting](#troubleshooting)
- [Adding Another Language](#adding-another-language)

## Runtime Overrides
Environment variables supersede config section values:
| Variable | Example | Description |
|----------|---------|-------------|
| `RECEPT_DEFAULT_CULTURE` | `sv-SE` | Default thread + request culture (falls back to `en-US` if invalid) |
| `RECEPT_SUPPORTED_CULTURES` | `sv-SE,en-US` | Comma/semicolon list; invalid codes skipped; if empty -> default only |

Example (PowerShell):
```powershell
$env:RECEPT_DEFAULT_CULTURE = 'sv-SE'
$env:RECEPT_SUPPORTED_CULTURES = 'sv-SE,en-US'
dotnet run --project ReceptRegister.Frontend
```

## Fixed Culture Configuration
Config section (per project):
```jsonc
"Localization": {
  "DefaultCulture": "en-US",
  "SupportedCultures": [ "en-US" ]
}
```
Only fixed provider registered today (no Accept-Language / cookie negotiation yet).

## Resource Files
Located in `ReceptRegister.Frontend/Resources/`:
- `SharedResources.resx` (default English)
- `SharedResources.sv.resx` (Swedish placeholders)

Key prefixes: Nav.*, Button.*, Table.*, Page.*, Form.*, A11y.*

## Adding UI Strings
1. Add key + value to default `.resx`.
2. Add same key to every other culture file (even if untranslated).
3. Inject and use: `@inject IStringLocalizer<SharedResources> L` then `@L["Button.Search"]`.

## Dynamic Strings
Format placeholders (`{0}`, `{1}`) enable localized grammar, e.g.:
```csharp
@string.Format(L["A11y.Recipes.ResultsCount"], total, pluralSuffix, page, totalPages)
```
Preserve placeholders exactly.

## Translation Workflow
1. Extract keys (done #99)
2. Provide Swedish translations (#100)
3. Add optional user language switch (#97 future)

## Troubleshooting
| Symptom | Cause | Fix |
|---------|-------|-----|
| Still English after change | App not restarted | Restart app
| Literal `{0}` output | Missing string.Format | Wrap resource in formatting
| Partial fallback to English | Key missing in culture file | Add missing key

## Adding Another Language
1. Copy `SharedResources.resx` to `SharedResources.<culture>.resx`
2. Translate values (keep placeholders)
3. Add culture code to `SupportedCultures`
4. Restart application

Roadmap: eventual user-selectable language + persisted preference.

---
← Previous: [Theming](./theming.md) | Next: [Glossary](./glossary.md) →
