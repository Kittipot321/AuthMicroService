using System.Security.Cryptography;
using System.Text;
using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Contracts.Common;
using AuthMicroservice.Core.Data;
using AuthMicroservice.Core.Domain;
using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Services;

internal sealed class RecoveryCodeService : IRecoveryCodeService
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly AuthDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IOptionsMonitor<AuthMicroserviceOptions> _authOptions;

    public RecoveryCodeService(
        AuthDbContext dbContext,
        IClock clock,
        IOptionsMonitor<AuthMicroserviceOptions> authOptions)
    {
        _dbContext = dbContext;
        _clock = clock;
        _authOptions = authOptions;
    }

    public async Task<AuthResult<IReadOnlyList<string>>> GenerateAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var options = _authOptions.CurrentValue.RecoveryCodes;
        if (!options.Enabled)
        {
            return AuthResult<IReadOnlyList<string>>.Failure(AuthErrorCodes.RecoveryCodesDisabled, "Recovery codes are disabled.");
        }

        var now = _clock.UtcNow;

        var existing = await _dbContext.TwoFactorRecoveryCodes
            .Where(r => r.UserId == userId && r.ConsumedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var stale in existing)
        {
            stale.ConsumedAt = now;
        }

        var plain = new List<string>(options.Count);
        for (var i = 0; i < options.Count; i++)
        {
            var raw = GenerateRawCode(options.Length);
            plain.Add(FormatForDisplay(raw));

            var salt = GenerateSalt();
            var hash = HashCode(Normalize(raw), salt);
            _dbContext.TwoFactorRecoveryCodes.Add(new TwoFactorRecoveryCode
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CodeHash = hash,
                Salt = salt,
                CreatedAt = now,
                IpAddress = ipAddress
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return AuthResult<IReadOnlyList<string>>.Success(plain);
    }

    public async Task<AuthResult> VerifyAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return AuthResult.Failure(AuthErrorCodes.InvalidRecoveryCode, "Recovery code is required.");
        }

        var options = _authOptions.CurrentValue.RecoveryCodes;
        if (!options.Enabled)
        {
            return AuthResult.Failure(AuthErrorCodes.RecoveryCodesDisabled, "Recovery codes are disabled.");
        }

        var normalized = Normalize(code);
        var active = await _dbContext.TwoFactorRecoveryCodes
            .Where(r => r.UserId == userId && r.ConsumedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var entry in active)
        {
            var candidate = HashCode(normalized, entry.Salt);
            if (FixedTimeEquals(candidate, entry.CodeHash))
            {
                entry.ConsumedAt = _clock.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return AuthResult.Success();
            }
        }

        return AuthResult.Failure(AuthErrorCodes.InvalidRecoveryCode, "Invalid or already-used recovery code.");
    }

    public Task<int> CountRemainingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TwoFactorRecoveryCodes
            .Where(r => r.UserId == userId && r.ConsumedAt == null)
            .CountAsync(cancellationToken);
    }

    private static string GenerateRawCode(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        }
        return new string(chars);
    }

    private static string FormatForDisplay(string raw)
    {
        if (raw.Length < 6)
        {
            return raw;
        }
        var mid = raw.Length / 2;
        return $"{raw[..mid]}-{raw[mid..]}";
    }

    private static string Normalize(string code)
    {
        var upper = code.Trim().ToUpperInvariant();
        var builder = new StringBuilder(upper.Length);
        foreach (var c in upper)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }

    private static string GenerateSalt()
    {
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer);
    }

    private static string HashCode(string code, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var codeBytes = Encoding.UTF8.GetBytes(code);
        var combined = new byte[saltBytes.Length + codeBytes.Length];
        Buffer.BlockCopy(saltBytes, 0, combined, 0, saltBytes.Length);
        Buffer.BlockCopy(codeBytes, 0, combined, saltBytes.Length, codeBytes.Length);
        var hash = SHA256.HashData(combined);
        return Convert.ToBase64String(hash);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
