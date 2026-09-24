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

public class OtpServiceTests
{
    private static (AuthDbContext ctx, OtpService svc, Mock<IClock> clock, ApplicationUser user, AuthMicroserviceOptions options)
        BuildEnv(Action<OtpOptions>? configure = null)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase("otp-tests-" + Guid.NewGuid())
            .Options;
        var ctx = new AuthDbContext(options);

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var otpOptions = new OtpOptions
        {
            CodeLength = 6,
            ExpirationMinutes = 10,
            MaxAttempts = 3,
            ResendCooldownSeconds = 0
        };
        configure?.Invoke(otpOptions);

        var authOptions = new AuthMicroserviceOptions { Otp = otpOptions };
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

        return (ctx, new OtpService(ctx, clock.Object, monitor.Object), clock, user, authOptions);
    }

    [Fact]
    public async Task Generate_ReturnsSixDigitCode_AndPersistsHashed()
    {
        var (ctx, svc, _, user, _) = BuildEnv();

        var result = await svc.GenerateAsync(user.Id, OtpPurpose.EmailVerification, "1.1.1.1", CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().MatchRegex("^[0-9]{6}$");

        var stored = ctx.OtpCodes.Single();
        stored.CodeHash.Should().NotBe(result.Value);
        stored.CodeHash.Should().NotBeNullOrEmpty();
        stored.Salt.Should().NotBeNullOrEmpty();
        stored.Purpose.Should().Be(OtpPurpose.EmailVerification);
        stored.IpAddress.Should().Be("1.1.1.1");
        stored.ConsumedAt.Should().BeNull();
    }

    [Fact]
    public async Task Generate_InvalidatesPreviousUnconsumedCodes_ForSameUserAndPurpose()
    {
        var (ctx, svc, _, user, _) = BuildEnv();

        await svc.GenerateAsync(user.Id, OtpPurpose.EmailVerification, null, CancellationToken.None);
        await svc.GenerateAsync(user.Id, OtpPurpose.EmailVerification, null, CancellationToken.None);

        var codes = ctx.OtpCodes.Where(o => o.UserId == user.Id).ToList();
        codes.Should().HaveCount(2);
        codes.Count(c => c.ConsumedAt == null).Should().Be(1);
    }

    [Fact]
    public async Task Generate_EnforcesCooldown_WhenLastCodeStillActive()
    {
        var (_, svc, _, user, _) = BuildEnv(o => o.ResendCooldownSeconds = 60);

        var first = await svc.GenerateAsync(user.Id, OtpPurpose.EmailVerification, null, CancellationToken.None);
        first.Succeeded.Should().BeTrue();

        var second = await svc.GenerateAsync(user.Id, OtpPurpose.EmailVerification, null, CancellationToken.None);
        second.Succeeded.Should().BeFalse();
        second.ErrorCode.Should().Be(AuthErrorCodes.OtpCooldownActive);
    }

    [Fact]
    public async Task Verify_CorrectCode_MarksConsumed_AndSucceeds()
    {
        var (ctx, svc, _, user, _) = BuildEnv();
        var gen = await svc.GenerateAsync(user.Id, OtpPurpose.EmailVerification, null, CancellationToken.None);

        var result = await svc.VerifyAsync(user.Id, OtpPurpose.EmailVerification, gen.Value!, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        ctx.OtpCodes.Single().ConsumedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Verify_WrongCode_IncrementsAttempts_AndReturnsInvalidOtp()
    {
        var (ctx, svc, _, user, _) = BuildEnv();
        await svc.GenerateAsync(user.Id, OtpPurpose.EmailVerification, null, CancellationToken.None);

        var result = await svc.VerifyAsync(user.Id, OtpPurpose.EmailVerification, "000000", CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.InvalidOtp);
        ctx.OtpCodes.Single().Attempts.Should().Be(1);
        ctx.OtpCodes.Single().ConsumedAt.Should().BeNull();
    }

    [Fact]
    public async Task Verify_MaxAttempts_ConsumesCode_AndReturnsAttemptsExceeded()
    {
        var (ctx, svc, _, user, _) = BuildEnv(o => o.MaxAttempts = 3);
        await svc.GenerateAsync(user.Id, OtpPurpose.EmailVerification, null, CancellationToken.None);

        await svc.VerifyAsync(user.Id, OtpPurpose.EmailVerification, "000000", CancellationToken.None);
        await svc.VerifyAsync(user.Id, OtpPurpose.EmailVerification, "000000", CancellationToken.None);
        var third = await svc.VerifyAsync(user.Id, OtpPurpose.EmailVerification, "000000", CancellationToken.None);

        third.Succeeded.Should().BeFalse();
        third.ErrorCode.Should().Be(AuthErrorCodes.OtpAttemptsExceeded);
        ctx.OtpCodes.Single().ConsumedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Verify_ExpiredCode_ReturnsOtpExpired()
    {
        var (ctx, svc, clock, user, _) = BuildEnv();
        var gen = await svc.GenerateAsync(user.Id, OtpPurpose.EmailVerification, null, CancellationToken.None);

        clock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 1, 1, 0, 20, 0, DateTimeKind.Utc));

        var result = await svc.VerifyAsync(user.Id, OtpPurpose.EmailVerification, gen.Value!, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.OtpExpired);
        ctx.OtpCodes.Single().ConsumedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Verify_NoActiveCode_ReturnsInvalidOtp()
    {
        var (_, svc, _, user, _) = BuildEnv();

        var result = await svc.VerifyAsync(user.Id, OtpPurpose.EmailVerification, "123456", CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.InvalidOtp);
    }

    [Fact]
    public async Task Verify_ConsumedCode_CannotBeReusedTwice()
    {
        var (_, svc, _, user, _) = BuildEnv();
        var gen = await svc.GenerateAsync(user.Id, OtpPurpose.EmailVerification, null, CancellationToken.None);

        var first = await svc.VerifyAsync(user.Id, OtpPurpose.EmailVerification, gen.Value!, CancellationToken.None);
        var second = await svc.VerifyAsync(user.Id, OtpPurpose.EmailVerification, gen.Value!, CancellationToken.None);

        first.Succeeded.Should().BeTrue();
        second.Succeeded.Should().BeFalse();
        second.ErrorCode.Should().Be(AuthErrorCodes.InvalidOtp);
    }

    [Fact]
    public async Task Verify_DifferentPurpose_DoesNotMatchCode()
    {
        var (_, svc, _, user, _) = BuildEnv();
        var gen = await svc.GenerateAsync(user.Id, OtpPurpose.EmailVerification, null, CancellationToken.None);

        var wrongPurpose = await svc.VerifyAsync(user.Id, OtpPurpose.LoginTwoFactor, gen.Value!, CancellationToken.None);

        wrongPurpose.Succeeded.Should().BeFalse();
        wrongPurpose.ErrorCode.Should().Be(AuthErrorCodes.InvalidOtp);
    }
}
