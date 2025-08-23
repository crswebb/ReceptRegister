using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using ReceptRegister.Api.Data;

namespace ReceptRegister.Api.Auth;

public interface IPasswordHasher
{
	(int iterations, byte[] salt, byte[] hash) Hash(string password, string? pepper = null);
	bool Verify(string password, string? pepper, byte[] salt, int iterations, byte[] expectedHash);
}

public class Pbkdf2PasswordHasher : IPasswordHasher
{
	private readonly ILogger<Pbkdf2PasswordHasher> _logger;
	private readonly int _defaultIterations;
	private readonly int _saltSize;
	private readonly int _keySize;
	private readonly AuthOptions _options;

	public Pbkdf2PasswordHasher(ILogger<Pbkdf2PasswordHasher> logger, IConfiguration config, AuthOptions options)
	{
		_logger = logger;
		_options = options;
		_defaultIterations = config.GetValue("RECEPT_PBKDF2_ITERATIONS", 150_000);
		_saltSize = 32;
		_keySize = 32; // 256-bit
		if (_defaultIterations < 50_000)
			_logger.LogWarning("PBKDF2 iteration count {Iterations} is low; consider raising (env RECEPT_PBKDF2_ITERATIONS)", _defaultIterations);
	}

	public (int iterations, byte[] salt, byte[] hash) Hash(string password, string? pepper = null)
	{
		var salt = RandomNumberGenerator.GetBytes(_saltSize);
		var key = Derive(password, pepper ?? _options.Pepper, salt, _defaultIterations, _keySize);
		return (_defaultIterations, salt, key);
	}

	public bool Verify(string password, string? pepper, byte[] salt, int iterations, byte[] expectedHash)
	{
		var actual = Derive(password, pepper ?? _options.Pepper, salt, iterations, expectedHash.Length);
		return CryptographicOperations.FixedTimeEquals(actual, expectedHash);
	}

	private static byte[] Derive(string password, string? pepper, byte[] salt, int iterations, int keySize)
	{
		var material = pepper is null ? password : password + pepper;
		// Use modern static API to avoid SYSLIB0060 warning
		// Fallback to positional parameters due to target framework preview API signature
		return Rfc2898DeriveBytes.Pbkdf2(material, salt, iterations, HashAlgorithmName.SHA256, keySize);
	}
}

public static class AuthServiceCollectionExtensions
{
	public static IServiceCollection AddAuthServices(this IServiceCollection services)
	{
		services.AddAuthOptions();
		services.AddSingleton<SessionSettings>(sp => SessionSettings.Load(sp.GetRequiredService<IConfiguration>()));
		services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
		services.AddScoped<IAuthRepository, AuthRepository>();
		services.AddScoped<IPasswordService, PasswordService>();
		services.AddSingleton<ISessionService, InMemorySessionService>();
		services.AddSingleton<ILoginRateLimiter, InMemoryLoginRateLimiter>();
		return services;
	}
}
