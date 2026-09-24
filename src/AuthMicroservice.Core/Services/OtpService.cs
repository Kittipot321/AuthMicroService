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

internal sealed class OtpService : IOtpService
{
    private readonly AuthDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IOptionsMonitor<AuthMicroserviceOptions> _authOptions;

    public OtpService(
        AuthDbContext dbContext,
        IClock clock,
        IOptionsMonitor<AuthMicroserviceOptions> authOptions)
    {
        _dbContext = dbContext;
        _clock = clock;
        _authOptions = authOptions;
    }

    public async Task<AuthResult<string>> GenerateAsync(Guid userId, OtpPurpose purpose, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var options = _authOptions.CurrentValue.Otp;
        var now = _clock.UtcNow;

        var latest = await _dbContext.OtpCodes
            .Where(o => o.UserId == userId && o.Purpose == purpose)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (latest is not null && latest.ConsumedAt is null && options.ResendCooldownSeconds > 0)
        {
            var elapsed = (now - latest.CreatedAt).TotalSeconds;
            if (elapsed < options.ResendCooldownSeconds)
            {
                var remaining = (int)Math.Ceiling(options.ResendCooldownSeconds - elapsed);
                return AuthResult<string>.Failure(
                    AuthErrorCodes.OtpCooldownActive,
                    $"Please wait {remaining} seconds before requesting another code.");
            }
        }

        var active = await _dbContext.OtpCodes
            .Where(o => o.UserId == userId && o.Purpose == purpose && o.ConsumedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var code in active)
        {
            code.ConsumedAt = now;
        }

        var rawCode = GenerateNumericCode(options.CodeLength);
        var salt = GenerateSalt();
        var hash = HashCode(rawCode, salt);

        var entity = new OtpCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CodeHash = hash,
            Salt = salt,
            Purpose = purpose,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(options.ExpirationMinutes),
            IpAddress = ipAddress
        };

        _dbContext.OtpCodes.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AuthResult<string>.Success(rawCode);
    }

    public async Task<AuthResult> VerifyAsync(Guid userId, OtpPurpose purpose, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return AuthResult.Failure(AuthErrorCodes.InvalidOtp, "Code is required.");
        }

        var options = _authOptions.CurrentValue.Otp;
        var now = _clock.UtcNow;

        var active = await _dbContext.OtpCodes
            .Where(o => o.UserId == userId && o.Purpose == purpose && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (active is null)
        {
            return AuthResult.Failure(AuthErrorCodes.InvalidOtp, "No active code was found. Request a new one.");
        }

        if (active.Attempts >= options.MaxAttempts)
        {
            active.ConsumedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return AuthResult.Failure(AuthErrorCodes.OtpAttemptsExceeded, "Too many attempts. Request a new code.");
        }

        if (active.IsExpired(now))
        {
            active.ConsumedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return AuthResult.Failure(AuthErrorCodes.OtpExpired, "Code has expired. Request a new one.");
        }

        var candidate = HashCode(code, active.Salt);
        var expected = active.CodeHash;

        if (!FixedTimeEquals(candidate, expected))
        {
            active.Attempts++;
            var attemptsLeft = options.MaxAttempts - active.Attempts;
            if (attemptsLeft <= 0)
            {
                active.ConsumedAt = now;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return AuthResult.Failure(AuthErrorCodes.OtpAttemptsExceeded, "Too many attempts. Request a new code.");
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return AuthResult.Failure(AuthErrorCodes.InvalidOtp, $"Invalid code. {attemptsLeft} attempts left.");
        }

        active.ConsumedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return AuthResult.Success();
    }

    private static string GenerateNumericCode(int length)
    {
        var max = (int)Math.Pow(10, length);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString(new string('0', length), System.Globalization.CultureInfo.InvariantCulture);
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
