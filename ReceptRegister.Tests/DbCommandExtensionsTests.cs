using System.Data.Common;
using Microsoft.Data.Sqlite;
using ReceptRegister.Api.Data;

namespace ReceptRegister.Tests;

public class DbCommandExtensionsTests
{
    private static DbCommand NewCmd()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        return conn.CreateCommand();
    }

    [Fact]
    public void AddParam_Chains_And_Handles_Null()
    {
        using var cmd = NewCmd();
        cmd.AddParam("@a", 123)
           .AddParam("@b", null)
           .AddParam("@c", "text");

        Assert.Equal(3, cmd.Parameters.Count);
        Assert.Equal(123, cmd.Parameters[0].Value);
        Assert.Equal(DBNull.Value, cmd.Parameters[1].Value); // null converted
        Assert.Equal("text", cmd.Parameters[2].Value);
        Assert.Equal("@c", cmd.Parameters[2].ParameterName);
    }

    [Fact]
    public void AddParam_Allows_Reassignment_And_Fluent_Reuse()
    {
        using var cmd = NewCmd();
        var chained = cmd.AddParam("@x", 1);
        Assert.Same(cmd, chained); // fluent returns same instance
        cmd.AddParam("@y", 2).AddParam("@z", 3);
        Assert.Equal(3, cmd.Parameters.Count);
    }
}
