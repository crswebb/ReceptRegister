# Glossary

Key terms and their concise definitions.

| Term | Definition |
|------|------------|
| Taxonomy | Collective term for Categories and Keywords; both are many-to-many linked to recipes. |
| Tried | Boolean flag indicating whether the recipe has been baked/tested. |
| Dialect | Runtime abstraction (`IDatabaseDialect`) supplying provider-specific SQL fragments (identity retrieval, upserts, paging). |
| Schema Initializer | Implementation of `ISchemaInitializer` that provisions database schema if missing. |
| Migration Runner | One-shot process that copies data from a SQLite file into a SQL Server database. |
| Pepper | Secret string appended to a password prior to PBKDF2 hashing (defense in depth). |
| Iterations | PBKDF2 work factor controlling hash computation time for brute force resistance. |
| AuthConfig | Table storing the single password hash + salt + iteration metadata for admin access. |

---
← Previous: [Localization](./localization.md) | Next: (none)
