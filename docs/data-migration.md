# Data Migration & Database Providers

This document expands on database provider selection and the SQLite → SQL Server migration helper.

## Contents
- [Providers Overview](#providers-overview)
- [Configuration](#configuration)
- [Schema Initialization](#schema-initialization)
- [Dialect Abstraction](#dialect-abstraction)
- [Migration Helper Usage](#migration-helper-usage)
- [Safety & Idempotency](#safety--idempotency)
- [Limitations & Roadmap](#limitations--roadmap)

## Providers Overview
Supported providers:

| Provider | `Database:Provider` value | Status | Notes |
|----------|---------------------------|--------|-------|
| SQLite | `SQLite` (or omitted) | Stable | Default; creates local file `App_Data/receptregister.db`; no connection string used. |
| SQL Server / Azure SQL | `SqlServer` | Experimental | Requires `Database:ConnectionString`; environment overrides via `RECEPT_DB_PROVIDER` / `RECEPT_DB_CONNECTIONSTRING`. |

Select via config key `Database:Provider` or the environment variable `RECEPT_DB_PROVIDER` (takes precedence). When `SqlServer` is chosen a non-empty connection string (config or `RECEPT_DB_CONNECTIONSTRING`) is mandatory; otherwise startup fails fast.

## Configuration
`appsettings.json` (or environment variables overriding):
```json
{
  "Database": {
    "Provider": "SQLite",
    "ConnectionString": ""  
  }
}
```
When `Provider=SqlServer` a non-empty `ConnectionString` is required; the application will fail fast otherwise.

## Schema Initialization
`ISchemaInitializer` implementation selected at runtime:
- `SqliteSchemaInitializer` – create-if-not-exists DDL
- `SqlServerSchemaInitializer` – guarded DDL (`IF NOT EXISTS`)

Executed early during startup for both API & Frontend hosts.

## Dialect Abstraction
`IDatabaseDialect` provides SQL differences (identity retrieval, upserts, paging) so repositories remain mostly neutral. Some SQL Server edge optimizations may be pending.

## Migration Helper Usage
One-shot helper to migrate existing SQLite content to SQL Server.

Steps:
1. Configure target: `Database:Provider=SqlServer` + valid connection string.
2. Run API with extra argument:
   ```powershell
   dotnet run --project ReceptRegister.Api -- --migrate-sqlite="C:\\path\\receptregister.db"
   ```
3. Process:
   - Ensures target schema exists
   - Reads taxonomy & recipes from source file
   - Upserts taxonomy (skips existing names)
   - Inserts recipes (skips natural key duplicates: Name+Book+Page)
   - Recreates many-to-many links
4. Exits (does not start web server) on success.

## Safety & Idempotency
- Re-runnable: duplicates skipped
- Does not delete or truncate target tables
- Source file opened read-only

## Limitations & Roadmap
Current limitations:
- No reverse (SQL Server → SQLite)
- No incremental diff; full copy each run
- Password (`AuthConfig`) intentionally not migrated
- Loads all recipes into memory (acceptable for small libs)
- Lacks automated integration test (planned)

Planned / referenced issues:
- #118 integration test for `DataMigrationRunner`
- #119 optional `AuthConfig` migration
- #120 dry-run / summary mode

---
← Previous: [Security](./security.md) | Next: [Theming](./theming.md) →
