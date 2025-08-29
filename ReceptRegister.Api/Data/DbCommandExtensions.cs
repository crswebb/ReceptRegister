using System.Data.Common;

namespace ReceptRegister.Api.Data;

/// <summary>
/// Extension helpers for consistent DbParameter creation.
/// </summary>
public static class DbCommandExtensions
{
    public static DbCommand AddParam(this DbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
        return cmd;
    }
}
