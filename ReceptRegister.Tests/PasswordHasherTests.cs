using ReceptRegister.Api.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
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
    services.AddSingleton<IWebHostEnvironment>(new FakeEnv());
    services.AddAuthServices();
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

    [Fact]
    public void PepperInfluencesHash()
    {
    // Arrange two separate providers with different peppers but same iteration count
        var servicesA = new ServiceCollection();
        var cfgA = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            {"RECEPT_PBKDF2_ITERATIONS", "50000"},
            {"RECEPT_PEPPER", "pepperA"}
        }).Build();
        servicesA.AddSingleton<IConfiguration>(cfgA);
        servicesA.AddLogging();
    servicesA.AddSingleton<IWebHostEnvironment>(new FakeEnv());
    servicesA.AddAuthServices();
        var hasherA = servicesA.BuildServiceProvider().GetRequiredService<IPasswordHasher>();

        // Provider B with pepper B
        var servicesB = new ServiceCollection();
        var cfgB = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            {"RECEPT_PBKDF2_ITERATIONS", "50000"},
            {"RECEPT_PEPPER", "pepperB"}
        }).Build();
        servicesB.AddSingleton<IConfiguration>(cfgB);
        servicesB.AddLogging();
    servicesB.AddSingleton<IWebHostEnvironment>(new FakeEnv());
    servicesB.AddAuthServices();
        var hasherB = servicesB.BuildServiceProvider().GetRequiredService<IPasswordHasher>();

    // Act: hash using pepperA, then attempt verification using pepperB which should fail
    var (iterA, saltA, hashA) = hasherA.Hash("secret");
    Assert.True(hasherA.Verify("secret", null, saltA, iterA, hashA)); // own pepper succeeds
    Assert.False(hasherB.Verify("secret", null, saltA, iterA, hashA)); // different pepper should fail
    }
}

internal sealed class FakeEnv : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "Test";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = ".";
    public string EnvironmentName { get; set; } = "Development";
    public string WebRootPath { get; set; } = ".";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
