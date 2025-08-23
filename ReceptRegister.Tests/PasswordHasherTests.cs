using ReceptRegister.Api.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ReceptRegister.Tests;

public class PasswordHasherTests
{
    private IPasswordHasher Create()
    {
        var services = new ServiceCollection();
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            {"RECEPT_PBKDF2_ITERATIONS", "100000"}
        }).Build();
        services.AddSingleton<IConfiguration>(cfg);
        services.AddLogging(b => b.AddDebug());
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        return services.BuildServiceProvider().GetRequiredService<IPasswordHasher>();
    }

    [Fact]
    public void HashAndVerify_Roundtrip_Succeeds()
    {
        var hasher = Create();
        var (iterations, salt, hash) = hasher.Hash("CorrectHorseBatteryStaple", pepper: "pep");
        Assert.True(iterations >= 100000);
        Assert.True(salt.Length >= 16);
        Assert.True(hasher.Verify("CorrectHorseBatteryStaple", "pep", salt, iterations, hash));
        Assert.False(hasher.Verify("wrong", "pep", salt, iterations, hash));
    }

    [Fact]
    public void DifferentSalt_DifferentHash()
    {
        var hasher = Create();
        var (_, salt1, hash1) = hasher.Hash("pw");
        var (_, salt2, hash2) = hasher.Hash("pw");
        Assert.False(salt1.SequenceEqual(salt2));
        Assert.False(hash1.SequenceEqual(hash2));
    }
}
