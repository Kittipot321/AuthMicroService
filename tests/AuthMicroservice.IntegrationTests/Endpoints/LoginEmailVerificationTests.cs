using System.Net;
using System.Net.Http.Json;
using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Contracts.Common;
using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Contracts.Responses;
using AuthMicroservice.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AuthMicroservice.IntegrationTests.Endpoints;

public class LoginEmailVerificationTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public LoginEmailVerificationTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WhenEmailUnverified_LinkMode_Returns202WithLinkChallenge()
    {
        _factory.EmailSender.Clear();
        using var factory = BuildFactory(mode: "Link", resendCooldownSeconds: 0);
        var client = factory.CreateClient();
        var email = $"unverified-link-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!",
            FullName = "Link Mode User"
        });
        _factory.EmailSender.Clear();

        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = "P@ssw0rd!" });

        login.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await login.Content.ReadFromJsonAsync<EmailVerificationRequiredResponse>();
        body!.Email.Should().Be(email);
        body.Mode.Should().Be(EmailVerificationMode.Link);
        body.VerificationSent.Should().BeTrue();
        body.ExpiresAt.Should().BeNull();
        _factory.EmailSender.Messages.Should().ContainSingle(m => m.To == email);
    }

    [Fact]
    public async Task Login_WhenEmailUnverified_CodeMode_Returns202WithCodeChallenge()
    {
        _factory.EmailSender.Clear();
        using var factory = BuildFactory(mode: "Code", resendCooldownSeconds: 0);
        var client = factory.CreateClient();
        var email = $"unverified-code-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!",
            FullName = "Code Mode User"
        });
        _factory.EmailSender.Clear();

        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = "P@ssw0rd!" });

        login.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await login.Content.ReadFromJsonAsync<EmailVerificationRequiredResponse>();
        body!.Email.Should().Be(email);
        body.Mode.Should().Be(EmailVerificationMode.Code);
        body.VerificationSent.Should().BeTrue();
        body.ExpiresAt.Should().NotBeNull();
        body.ExpiresAt!.Value.Should().BeAfter(DateTime.UtcNow.AddSeconds(-5));
        _factory.EmailSender.Messages.Should().ContainSingle(m => m.To == email);
    }

    [Fact]
    public async Task Login_WhenEmailUnverified_CodeMode_RespectsCooldown()
    {
        _factory.EmailSender.Clear();
        using var factory = BuildFactory(mode: "Code", resendCooldownSeconds: 60);
        var client = factory.CreateClient();
        var email = $"unverified-cooldown-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!",
            FullName = "Cooldown User"
        });
        _factory.EmailSender.Messages.Count(m => m.To == email).Should().Be(1, "register dispatches the first OTP");

        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = "P@ssw0rd!" });

        login.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await login.Content.ReadFromJsonAsync<EmailVerificationRequiredResponse>();
        body!.Mode.Should().Be(EmailVerificationMode.Code);
        body.VerificationSent.Should().BeFalse("cooldown from register is still active");
        body.ExpiresAt.Should().BeNull();
        _factory.EmailSender.Messages.Count(m => m.To == email).Should().Be(1, "login must not send a second OTP while cooldown is active");
    }

    [Fact]
    public async Task VerifyEmailOtp_WhenModeLink_ReturnsOtpDisabled()
    {
        _factory.EmailSender.Clear();
        using var factory = BuildFactory(mode: "Link", resendCooldownSeconds: 0);
        var client = factory.CreateClient();
        var email = $"link-mode-otp-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!",
            FullName = "Link Mode OTP User"
        });

        var verify = await client.PostAsJsonAsync("/auth/otp/email/verify", new VerifyEmailOtpRequest
        {
            Email = email,
            Code = "123456"
        });

        verify.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await verify.Content.ReadAsStringAsync();
        problem.Should().Contain(AuthErrorCodes.OtpDisabled);
    }

    private WebApplicationFactory<Program> BuildFactory(string mode, int resendCooldownSeconds)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AuthMicroservice:EmailVerification:Mode"] = mode,
                    ["AuthMicroservice:Otp:ResendCooldownSeconds"] = resendCooldownSeconds.ToString()
                });
            });
        });
    }
}
