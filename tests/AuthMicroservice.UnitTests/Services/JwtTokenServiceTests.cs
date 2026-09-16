using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Domain;
using AuthMicroservice.Core.Services;
using AuthMicroservice.Core.Services.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace AuthMicroservice.UnitTests.Services;

public class JwtTokenServiceTests
{
    private static AuthMicroserviceOptions DefaultOptions() => new()
    {
        Jwt = new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            Key = "unit-test-signing-key-that-is-at-least-32-chars",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeDays = 7,
            ClockSkewSeconds = 30
        }
    };

    private static (JwtTokenService svc, DateTime now) BuildService(AuthMicroserviceOptions? opts = null)
    {
        var options = Options.Create(opts ?? DefaultOptions());
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(now);
        return (new JwtTokenService(options, clock.Object), now);
    }

    [Fact]
    public void GenerateAccessToken_ProducesTokenWithExpectedClaims()
    {
        var (svc, now) = BuildService();
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "alice@example.com", UserName = "alice@example.com" };

        var access = svc.GenerateAccessToken(user, new[] { "User", "Admin" });

        access.Token.Should().NotBeNullOrEmpty();
        access.Jti.Should().NotBeNullOrEmpty();
        access.ExpiresAt.Should().Be(now.AddMinutes(15));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(access.Token);
        jwt.Issuer.Should().Be("test-issuer");
        jwt.Audiences.Should().Contain("test-audience");

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "alice@example.com");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti && c.Value == access.Jti);
        jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value)
            .Should().BeEquivalentTo(new[] { "User", "Admin" });
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsUniqueBase64UrlValues()
    {
        var (svc, _) = BuildService();

        var a = svc.GenerateRefreshToken();
        var b = svc.GenerateRefreshToken();

        a.Should().NotBeNullOrEmpty();
        b.Should().NotBeNullOrEmpty();
        a.Should().NotBe(b);
        a.Should().NotContain("=");
        a.Should().NotContain("+");
        a.Should().NotContain("/");
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ValidatesSignatureButIgnoresExpiry()
    {
        var opts = DefaultOptions();
        opts.Jwt.AccessTokenLifetimeMinutes = 1;

        var (svc, _) = BuildService(opts);
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "bob@example.com", UserName = "bob@example.com" };

        var access = svc.GenerateAccessToken(user, Array.Empty<string>());

        var principal = svc.GetPrincipalFromExpiredToken(access.Token);
        principal.Should().NotBeNull();
        // JwtSecurityTokenHandler's default InboundClaimTypeMap maps "sub" → NameIdentifier.
        principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(user.Id.ToString());
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ReturnsNullForTamperedToken()
    {
        var (svc, _) = BuildService();
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "eve@example.com", UserName = "eve@example.com" };
        var access = svc.GenerateAccessToken(user, Array.Empty<string>());

        var tampered = access.Token.Substring(0, access.Token.Length - 4) + "AAAA";

        svc.GetPrincipalFromExpiredToken(tampered).Should().BeNull();
    }
}
