using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Data;
using AuthMicroservice.Core.Domain;
using AuthMicroservice.Core.Services;
using AuthMicroservice.Core.Services.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace AuthMicroservice.UnitTests.Services;

public class RefreshTokenServiceTests
{
    private static (AuthDbContext ctx, RefreshTokenService svc, Mock<IClock> clock, ApplicationUser user) BuildEnv()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase("refresh-token-tests-" + Guid.NewGuid())
            .Options;
        var ctx = new AuthDbContext(options);

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var jwtSvc = new Mock<IJwtTokenService>();
        jwtSvc.Setup(j => j.GenerateRefreshToken())
            .Returns(() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));

        var authOptions = Options.Create(new AuthMicroserviceOptions
        {
            Jwt = new JwtOptions { RefreshTokenLifetimeDays = 7, Key = new string('a', 32) }
        });

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "u@example.com",
            UserName = "u@example.com"
        };
        ctx.Users.Add(user);
        ctx.SaveChanges();

        return (ctx, new RefreshTokenService(ctx, jwtSvc.Object, clock.Object, authOptions), clock, user);
    }

    [Fact]
    public async Task IssueAsync_PersistsHashNotRawToken()
    {
        var (ctx, svc, _, user) = BuildEnv();

        var raw = await svc.IssueAsync(user, "jwt-1", "127.0.0.1");

        raw.Should().NotBeNullOrEmpty();
        var stored = ctx.RefreshTokens.Single();
        stored.TokenHash.Should().NotBe(raw);
        stored.TokenHash.Should().Be(RefreshTokenService.HashToken(raw));
        stored.UserId.Should().Be(user.Id);
        stored.JwtId.Should().Be("jwt-1");
    }

    [Fact]
    public async Task RotateAsync_RevokesOldAndIssuesNew_LinkedByReplacedBy()
    {
        var (ctx, svc, _, user) = BuildEnv();
        var raw = await svc.IssueAsync(user, "jwt-1", "1.1.1.1");

        var result = await svc.RotateAsync(raw, "jwt-2", "2.2.2.2");

        result.Succeeded.Should().BeTrue();
        result.NewRawToken.Should().NotBeNullOrEmpty();
        result.User!.Id.Should().Be(user.Id);

        var oldHash = RefreshTokenService.HashToken(raw);
        var newHash = RefreshTokenService.HashToken(result.NewRawToken!);

        var oldEntity = ctx.RefreshTokens.Single(t => t.TokenHash == oldHash);
        oldEntity.RevokedAt.Should().NotBeNull();
        oldEntity.ReplacedByTokenHash.Should().Be(newHash);

        var newEntity = ctx.RefreshTokens.Single(t => t.TokenHash == newHash);
        newEntity.JwtId.Should().Be("jwt-2");
        newEntity.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task RotateAsync_RejectsExpiredToken()
    {
        var (ctx, svc, clock, user) = BuildEnv();
        var raw = await svc.IssueAsync(user, "jwt-1", null);

        clock.Setup(c => c.UtcNow).Returns(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await svc.RotateAsync(raw, "jwt-2", null);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RotateAsync_UsingAlreadyRotatedToken_RevokesDescendants()
    {
        var (ctx, svc, _, user) = BuildEnv();
        var raw1 = await svc.IssueAsync(user, "jwt-1", null);
        var rotation = await svc.RotateAsync(raw1, "jwt-2", null);
        var raw2 = rotation.NewRawToken!;

        var replay = await svc.RotateAsync(raw1, "jwt-3", null);

        replay.Succeeded.Should().BeFalse();
        var hash2 = RefreshTokenService.HashToken(raw2);
        ctx.RefreshTokens.Single(t => t.TokenHash == hash2).RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeAllForUserAsync_RevokesAllActiveTokens()
    {
        var (ctx, svc, _, user) = BuildEnv();
        await svc.IssueAsync(user, "jwt-1", null);
        await svc.IssueAsync(user, "jwt-2", null);
        await svc.IssueAsync(user, "jwt-3", null);

        await svc.RevokeAllForUserAsync(user.Id, "9.9.9.9", "logout-all");

        ctx.RefreshTokens.Where(t => t.UserId == user.Id).ToList()
            .Should().OnlyContain(t => t.RevokedAt != null && t.ReasonRevoked == "logout-all");
    }
}
