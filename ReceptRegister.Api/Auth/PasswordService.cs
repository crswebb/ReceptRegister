using ReceptRegister.Api.Data;
using Microsoft.Extensions.Logging;

namespace ReceptRegister.Api.Auth;

public interface IPasswordService
{
    Task<bool> HasPasswordAsync(CancellationToken ct = default);
    Task SetPasswordAsync(string password, CancellationToken ct = default);
    Task<bool> VerifyAsync(string password, CancellationToken ct = default);
}

public sealed class PasswordService : IPasswordService
{
    private readonly IAuthRepository _repo;
    private readonly IPasswordHasher _hasher;
    private readonly ILogger<PasswordService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IConfiguration _config;
    private readonly AuthOptions _options;

    public PasswordService(IAuthRepository repo,
        IPasswordHasher hasher,
        ILogger<PasswordService> logger,
        TimeProvider timeProvider,
        IConfiguration config,
        AuthOptions options)
    {
        _repo = repo;
        _hasher = hasher;
        _logger = logger;
        _timeProvider = timeProvider;
        _config = config;
        _options = options;
    }

    public Task<bool> HasPasswordAsync(CancellationToken ct = default) => _repo.HasPasswordAsync(ct);

    public async Task SetPasswordAsync(string password, CancellationToken ct = default)
    {
        var (iterations, salt, hash) = _hasher.Hash(password); // pepper applied automatically by hasher
        var now = _timeProvider.GetUtcNow();
        await _repo.SetPasswordAsync(hash, salt, iterations, now, ct);
        _logger.LogInformation("Password set with iterations {Iterations}{PepperFlag}", iterations, _options.Pepper != null ? " and pepper" : string.Empty);
    }

    public async Task<bool> VerifyAsync(string password, CancellationToken ct = default)
    {
        var cfg = await _repo.GetAsync(ct);
        if (cfg is null) return false;
        if (_hasher.Verify(password, null, cfg.Salt, cfg.Iterations, cfg.PasswordHash))
        {
            // Optional on-login transparent upgrade if iteration env increased
            var targetIterations = _config.GetValue("RECEPT_PBKDF2_ITERATIONS", cfg.Iterations);
            if (targetIterations > cfg.Iterations)
            {
                var (newIter, salt, hash) = _hasher.Hash(password);
                var now = _timeProvider.GetUtcNow();
                await _repo.SetPasswordAsync(hash, salt, newIter, now, ct);
                _logger.LogInformation("Upgraded password hash iterations from {Old} to {New}", cfg.Iterations, newIter);
            }
            return true;
        }
        return false;
    }
}
