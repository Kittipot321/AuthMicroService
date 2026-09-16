using System.Security.Claims;
using AuthMicroservice.Core.Domain;

namespace AuthMicroservice.Core.Services.Abstractions;

public interface IJwtTokenService
{
    JwtAccessToken GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles, IEnumerable<Claim>? extraClaims = null);

    string GenerateRefreshToken();

    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}

public sealed record JwtAccessToken(string Token, string Jti, DateTime ExpiresAt);
