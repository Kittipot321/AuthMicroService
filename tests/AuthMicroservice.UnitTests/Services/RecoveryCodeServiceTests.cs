using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Contracts.Common;
using AuthMicroservice.Core.Data;
using AuthMicroservice.Core.Domain;
using AuthMicroservice.Core.Services;
using AuthMicroservice.Core.Services.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace AuthMicroservice.UnitTests.Services;

public class RecoveryCodeServiceTests
{
    private static (AuthDbContext ctx, RecoveryCodeService svc, ApplicationUser user) Build(
        Action<RecoveryCodeOptions>? configure = null)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase("recovery-tests-" + Guid.NewGuid())
            .Options;
        var ctx = new AuthDbContext(options);

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var recoveryOptions = new RecoveryCodeOptions { Enabled = true, Count = 10, Length = 10 };
        configure?.Invoke(recoveryOptions);

        var authOptions = new AuthMicroserviceOptions { RecoveryCodes = recoveryOptions };
        var monitor = new Mock<IOptionsMonitor<AuthMicroserviceOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(authOptions);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "u@example.com",
            UserName = "u@example.com"
        };
        ctx.Users.Add(user);
        ctx.SaveChanges();

        return (ctx, new RecoveryCodeService(ctx, clock.Object, monitor.Object), user);
    }

    [Fact]
    public async Task Generate_ReturnsRequestedNumberOfUniqueCodes()
    {
        var (ctx, svc, user) = Build();

        var result = await svc.GenerateAsync(user.Id, "1.1.1.1", CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Count.Should().Be(10);
        result.Value.Distinct().Count().Should().Be(10);
        ctx.TwoFactorRecoveryCodes.Count(r => r.UserId == user.Id && r.ConsumedAt == null).Should().Be(10);
    }

    [Fact]
    public async Task Generate_InvalidatesPreviouslyActiveCodes()
    {
        var (ctx, svc, user) = Build();

        await svc.GenerateAsync(user.Id, null, CancellationToken.None);
        await svc.GenerateAsync(user.Id, null, CancellationToken.None);

        ctx.TwoFactorRecoveryCodes.Count(r => r.UserId == user.Id).Should().Be(20);
        ctx.TwoFactorRecoveryCodes.Count(r => r.UserId == user.Id && r.ConsumedAt == null).Should().Be(10);
    }

    [Fact]
    public async Task Verify_ValidCode_MarksConsumed_AndSucceeds()
    {
        var (ctx, svc, user) = Build();
        var gen = await svc.GenerateAsync(user.Id, null, CancellationToken.None);
        var code = gen.Value![0];

        var result = await svc.VerifyAsync(user.Id, code, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        ctx.TwoFactorRecoveryCodes.Count(r => r.UserId == user.Id && r.ConsumedAt != null).Should().Be(1);
    }

    [Fact]
    public async Task Verify_SameCodeTwice_SecondCallFails()
    {
        var (_, svc, user) = Build();
        var gen = await svc.GenerateAsync(user.Id, null, CancellationToken.None);
        var code = gen.Value![0];

        var first = await svc.VerifyAsync(user.Id, code, CancellationToken.None);
        var second = await svc.VerifyAsync(user.Id, code, CancellationToken.None);

        first.Succeeded.Should().BeTrue();
        second.Succeeded.Should().BeFalse();
        second.ErrorCode.Should().Be(AuthErrorCodes.InvalidRecoveryCode);
    }

    [Fact]
    public async Task Verify_UnknownCode_ReturnsInvalidRecoveryCode()
    {
        var (_, svc, user) = Build();
        await svc.GenerateAsync(user.Id, null, CancellationToken.None);

        var result = await svc.VerifyAsync(user.Id, "AAAA-BBBB", CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.InvalidRecoveryCode);
    }

    [Fact]
    public async Task Verify_IgnoresDashesAndCase()
    {
        var (_, svc, user) = Build();
        var gen = await svc.GenerateAsync(user.Id, null, CancellationToken.None);
        var code = gen.Value![0];
        var mangled = code.Replace("-", string.Empty).ToLowerInvariant();

        var result = await svc.VerifyAsync(user.Id, mangled, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Generate_DisabledOption_ReturnsFailure()
    {
        var (_, svc, user) = Build(o => o.Enabled = false);

        var result = await svc.GenerateAsync(user.Id, null, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.RecoveryCodesDisabled);
    }

    [Fact]
    public async Task CountRemaining_ReturnsUnconsumedCount()
    {
        var (_, svc, user) = Build();
        var gen = await svc.GenerateAsync(user.Id, null, CancellationToken.None);

        await svc.VerifyAsync(user.Id, gen.Value![0], CancellationToken.None);

        var remaining = await svc.CountRemainingAsync(user.Id, CancellationToken.None);
        remaining.Should().Be(9);
    }
}
