// Example future migration placeholder.
// To add a migration: duplicate this file, increment Id, adjust Name & SQL, commit.
namespace ReceptRegister.Api.Data.SchemaMigrations.Migrations;

public sealed class Sample_0002_AddExampleColumn : ISchemaMigration
{
    public int Id => 2; // > 1 (baseline)
    public string Name => "add-example-column";
    public string GetSql(string provider)
    {
        // Demonstration only: NOT applied if column already exists (would fail). Keep disabled by returning no-op.
        // Replace with safe ALTER statements guarded by IF / pragma checks as needed. For now return neutral SQL.
        return provider == "SqlServer" ? "-- no-op sample migration" : "-- no-op sample migration";
    }
}
