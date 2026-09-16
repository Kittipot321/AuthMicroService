using System.Security.Cryptography;
using System.Text;
using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Data;
using AuthMicroservice.Core.Domain;
using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Services;

internal sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly AuthDbContext _dbContext;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IClock _clock;
    private readonly JwtOptions _jwtOptions;

    public RefreshTokenService(
        AuthDbContext dbContext,
        IJwtTokenService jwtTokenService,
        IClock clock,
        IOptions<AuthMicroserviceOptions> options)
    {
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
        _clock = clock;
        _jwtOptions = options.Value.Jwt;
    }

    public async Task<string> IssueAsync(ApplicationUser user, string jwtId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var raw = _jwtTokenService.GenerateRefreshToken();
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(raw),
            JwtId = jwtId,
            CreatedAt = _clock.UtcNow,
            ExpiresAt = _clock.UtcNow.AddDays(_jwtOptions.RefreshTokenLifetimeDays),
            CreatedByIp = ipAddress
        };

        _dbContext.RefreshTokens.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return raw;
    }

    public async Task<RefreshTokenRotationResult> RotateAsync(string rawToken, string newJwtId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return new RefreshTokenRotationResult(false, null, null, "Refresh token is required.");
        }

        var hash = HashToken(rawToken);
        var stored = await _dbContext.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return new RefreshTokenRotationResult(false, null, null, "Refresh token not found.");
        }

        var now = _clock.UtcNow;
        if (!stored.IsActive(now))
        {
            if (!stored.IsRevoked)
            {
                stored.RevokedAt = now;
                stored.RevokedByIp = ipAddress;
                stored.ReasonRevoked = "Attempted use of expired token.";
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await RevokeDescendantsAsync(stored, ipAddress, "Attempted reuse of revoked token — chain revoked.", cancellationToken)
                    .ConfigureAwait(false);
            }

            return new RefreshTokenRotationResult(false, null, null, "Refresh token is no longer valid.");
        }

        var newRaw = _jwtTokenService.GenerateRefreshToken();
        var replacement = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = stored.UserId,
            TokenHash = HashToken(newRaw),
            JwtId = newJwtId,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwtOptions.RefreshTokenLifetimeDays),
            CreatedByIp = ipAddress
        };

        stored.RevokedAt = now;
        stored.RevokedByIp = ipAddress;
        stored.ReasonRevoked = "Rotated.";
        stored.ReplacedByTokenHash = replacement.TokenHash;

        _dbContext.RefreshTokens.Add(replacement);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new RefreshTokenRotationResult(true, stored.User, newRaw, null);
    }

    public async Task RevokeAsync(string rawToken, string? ipAddress, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }

        var hash = HashToken(rawToken);
        var stored = await _dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);
        if (stored is null || stored.IsRevoked)
        {
            return;
        }

        stored.RevokedAt = _clock.UtcNow;
        stored.RevokedByIp = ipAddress;
        stored.ReasonRevoked = reason;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RevokeAllForUserAsync(Guid userId, string? ipAddress, string reason, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var tokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var token in tokens)
        {
            token.RevokedAt = now;
            token.RevokedByIp = ipAddress;
            token.ReasonRevoked = reason;
        }

        if (tokens.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RevokeDescendantsAsync(RefreshToken token, string? ipAddress, string reason, CancellationToken cancellationToken)
    {
        var current = token;
        var now = _clock.UtcNow;
        while (!string.IsNullOrEmpty(current.ReplacedByTokenHash))
        {
            var next = await _dbContext.RefreshTokens
                .SingleOrDefaultAsync(t => t.TokenHash == current.ReplacedByTokenHash, cancellationToken)
                .ConfigureAwait(false);
            if (next is null)
            {
                break;
            }

            if (!next.IsRevoked)
            {
                next.RevokedAt = now;
                next.RevokedByIp = ipAddress;
                next.ReasonRevoked = reason;
            }

            current = next;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}
