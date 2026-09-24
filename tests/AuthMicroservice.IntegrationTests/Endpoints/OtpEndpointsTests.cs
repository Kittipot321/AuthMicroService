using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AuthMicroservice.Core.Contracts.Common;
using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Contracts.Responses;
using AuthMicroservice.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace AuthMicroservice.IntegrationTests.Endpoints;

public class OtpEndpointsTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public OtpEndpointsTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EmailVerification_SendAndVerifyOtp_ConfirmsEmail()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"otp-email-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!"
        });
        _factory.EmailSender.Clear();

        var send = await client.PostAsJsonAsync("/auth/otp/email/send", new SendEmailVerificationOtpRequest { Email = email });
        send.StatusCode.Should().Be(HttpStatusCode.OK);

        var code = ExtractCode(_factory.EmailSender.Messages.Should().ContainSingle().Subject.HtmlBody);
        code.Should().NotBeNullOrEmpty();

        var verify = await client.PostAsJsonAsync("/auth/otp/email/verify", new VerifyEmailOtpRequest
        {
            Email = email,
            Code = code!
        });
        verify.StatusCode.Should().Be(HttpStatusCode.OK);

        var repeat = await client.PostAsJsonAsync("/auth/otp/email/verify", new VerifyEmailOtpRequest
        {
            Email = email,
            Code = code!
        });
        repeat.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VerifyEmailOtp_WrongCode_ReturnsUnauthorized()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"otp-wrong-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!"
        });
        await client.PostAsJsonAsync("/auth/otp/email/send", new SendEmailVerificationOtpRequest { Email = email });

        var verify = await client.PostAsJsonAsync("/auth/otp/email/verify", new VerifyEmailOtpRequest
        {
            Email = email,
            Code = "000000"
        });
        verify.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginWith2FaEnabled_ReturnsAccepted_ThenOtpVerifyReturnsTokens()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"otp-2fa-{Guid.NewGuid():N}@example.com";
        const string password = "P@ssw0rd!";

        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password
        });
        var tokens = await register.Content.ReadFromJsonAsync<AuthResponse>();
        _factory.EmailSender.Clear();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var enableRequest = await client.PostAsync("/auth/2fa/enable-request", content: null);
        enableRequest.StatusCode.Should().Be(HttpStatusCode.OK);

        var enableCode = ExtractCode(_factory.EmailSender.Messages.Should().ContainSingle().Subject.HtmlBody);
        _factory.EmailSender.Clear();

        var enableConfirm = await client.PostAsJsonAsync("/auth/2fa/enable-confirm", new Enable2FaConfirmRequest { Code = enableCode! });
        enableConfirm.StatusCode.Should().Be(HttpStatusCode.NoContent);

        client.DefaultRequestHeaders.Authorization = null;

        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        login.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var loginCode = ExtractCode(_factory.EmailSender.Messages.Should().ContainSingle().Subject.HtmlBody);
        loginCode.Should().NotBeNullOrEmpty();

        var verify = await client.PostAsJsonAsync("/auth/login/2fa/verify", new LoginTwoFactorRequest
        {
            Email = email,
            Code = loginCode!
        });
        verify.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResponse = await verify.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse!.AccessToken.Should().NotBeNullOrEmpty();
        authResponse.User.Email.Should().Be(email);
    }

    [Fact]
    public async Task Disable2Fa_WithWrongPassword_ReturnsUnauthorized()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"otp-disable-{Guid.NewGuid():N}@example.com";
        const string password = "P@ssw0rd!";

        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password
        });
        var tokens = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        _factory.EmailSender.Clear();
        await client.PostAsync("/auth/2fa/enable-request", content: null);
        var enableCode = ExtractCode(_factory.EmailSender.Messages.Should().ContainSingle().Subject.HtmlBody);
        await client.PostAsJsonAsync("/auth/2fa/enable-confirm", new Enable2FaConfirmRequest { Code = enableCode! });

        var disable = await client.PostAsJsonAsync("/auth/2fa/disable", new Disable2FaRequest { Password = "wrong-password" });
        disable.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SendOtp_ForNonexistentEmail_ReturnsOk_SilentSuccess()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();

        var send = await client.PostAsJsonAsync("/auth/otp/email/send", new SendEmailVerificationOtpRequest
        {
            Email = $"nobody-{Guid.NewGuid():N}@example.com"
        });

        send.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.EmailSender.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Enable2Fa_Confirm_WithInvalidCode_ReturnsUnauthorized()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"otp-enable-bad-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!"
        });
        var tokens = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        _factory.EmailSender.Clear();
        await client.PostAsync("/auth/2fa/enable-request", content: null);

        var confirm = await client.PostAsJsonAsync("/auth/2fa/enable-confirm", new Enable2FaConfirmRequest { Code = "000000" });
        confirm.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string? ExtractCode(string html)
    {
        var match = Regex.Match(html, @">\s*(\d{6})\s*<");
        return match.Success ? match.Groups[1].Value : null;
    }
}
