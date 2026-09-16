using AuthMicroservice.Core.Domain;

namespace AuthMicroservice.Core.Services.Abstractions;

public interface IRefreshTokenService
{
    Task<string> IssueAsync(ApplicationUser user, string jwtId, string? ipAddress, CancellationToken cancellationToken = default);

    Task<RefreshTokenRotationResult> RotateAsync(string rawToken, string newJwtId, string? ipAddress, CancellationToken cancellationToken = default);

    Task RevokeAsync(string rawToken, string? ipAddress, string reason, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(Guid userId, string? ipAddress, string reason, CancellationToken cancellationToken = default);
}

public sealed record RefreshTokenRotationResult(bool Succeeded, ApplicationUser? User, string? NewRawToken, string? ErrorMessage);
