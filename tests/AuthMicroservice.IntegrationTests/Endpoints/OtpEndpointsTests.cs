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
        repeat.StatusCode.Should().Be(HttpStatusCode.Conflict);
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

    [Fact]
    public async Task LoginTwoFactorVerify_WithWrongCode_ReturnsUnauthorized()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"otp-2fa-wrong-{Guid.NewGuid():N}@example.com";
        const string password = "P@ssw0rd!";

        await EnableTwoFactorAsync(client, email, password);

        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        login.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var verify = await client.PostAsJsonAsync("/auth/login/2fa/verify", new LoginTwoFactorRequest
        {
            Email = email,
            Code = "000000"
        });
        verify.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginTwoFactorVerify_ReplayAfterSuccess_ReturnsUnauthorized()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"otp-2fa-replay-{Guid.NewGuid():N}@example.com";
        const string password = "P@ssw0rd!";

        await EnableTwoFactorAsync(client, email, password);

        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        login.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var loginCode = ExtractCode(_factory.EmailSender.Messages.Should().ContainSingle().Subject.HtmlBody);
        loginCode.Should().NotBeNullOrEmpty();

        var firstVerify = await client.PostAsJsonAsync("/auth/login/2fa/verify", new LoginTwoFactorRequest
        {
            Email = email,
            Code = loginCode!
        });
        firstVerify.StatusCode.Should().Be(HttpStatusCode.OK);

        var replay = await client.PostAsJsonAsync("/auth/login/2fa/verify", new LoginTwoFactorRequest
        {
            Email = email,
            Code = loginCode!
        });
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginTwoFactorVerify_When2FaNotEnabled_ReturnsUnauthorized()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"otp-2fa-nosession-{Guid.NewGuid():N}@example.com";
        const string password = "P@ssw0rd!";

        await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password
        });

        var verify = await client.PostAsJsonAsync("/auth/login/2fa/verify", new LoginTwoFactorRequest
        {
            Email = email,
            Code = "123456"
        });
        verify.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginTwoFactorVerify_WithEmailVerificationCode_ReturnsUnauthorized()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"otp-2fa-crosspurpose-{Guid.NewGuid():N}@example.com";
        const string password = "P@ssw0rd!";

        await EnableTwoFactorAsync(client, email, password);

        _factory.EmailSender.Clear();
        var sendEmailVerification = await client.PostAsJsonAsync("/auth/otp/email/send", new SendEmailVerificationOtpRequest { Email = email });
        sendEmailVerification.StatusCode.Should().Be(HttpStatusCode.OK);
        var emailVerificationCode = ExtractCode(_factory.EmailSender.Messages.Should().ContainSingle().Subject.HtmlBody);
        emailVerificationCode.Should().NotBeNullOrEmpty();

        _factory.EmailSender.Clear();
        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        login.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var verify = await client.PostAsJsonAsync("/auth/login/2fa/verify", new LoginTwoFactorRequest
        {
            Email = email,
            Code = emailVerificationCode!
        });
        verify.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Enable2FaRequest_WhenAlreadyEnabled_ReturnsConflict()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"otp-2fa-dup-{Guid.NewGuid():N}@example.com";
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

        var secondEnable = await client.PostAsync("/auth/2fa/enable-request", content: null);
        secondEnable.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Disable2Fa_WithCorrectPassword_ReturnsNoContent()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"otp-2fa-disable-ok-{Guid.NewGuid():N}@example.com";
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

        var disable = await client.PostAsJsonAsync("/auth/2fa/disable", new Disable2FaRequest { Password = password });
        disable.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Disable2Fa_When2FaNotEnabled_ReturnsConflict()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"otp-2fa-disable-noop-{Guid.NewGuid():N}@example.com";
        const string password = "P@ssw0rd!";

        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password
        });
        var tokens = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var disable = await client.PostAsJsonAsync("/auth/2fa/disable", new Disable2FaRequest { Password = password });
        disable.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task LoginTwoFactorResend_IssuesNewCode_AndOldCodeIsInvalidated()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"otp-2fa-resend-{Guid.NewGuid():N}@example.com";
        const string password = "P@ssw0rd!";

        await EnableTwoFactorAsync(client, email, password);

        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        login.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var firstCode = ExtractCode(_factory.EmailSender.Messages.Should().ContainSingle().Subject.HtmlBody);
        firstCode.Should().NotBeNullOrEmpty();
        _factory.EmailSender.Clear();

        var resend = await client.PostAsJsonAsync("/auth/login/2fa/resend", new SendLoginTwoFactorOtpRequest { Email = email });
        resend.StatusCode.Should().Be(HttpStatusCode.OK);
        var newCode = ExtractCode(_factory.EmailSender.Messages.Should().ContainSingle().Subject.HtmlBody);
        newCode.Should().NotBeNullOrEmpty();
        newCode.Should().NotBe(firstCode);

        var oldVerify = await client.PostAsJsonAsync("/auth/login/2fa/verify", new LoginTwoFactorRequest
        {
            Email = email,
            Code = firstCode!
        });
        oldVerify.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var newVerify = await client.PostAsJsonAsync("/auth/login/2fa/verify", new LoginTwoFactorRequest
        {
            Email = email,
            Code = newCode!
        });
        newVerify.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LoginTwoFactorResend_ForUserWithout2FaEnabled_ReturnsOk_SilentSuccess()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"otp-2fa-resend-no2fa-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!"
        });
        _factory.EmailSender.Clear();

        var resend = await client.PostAsJsonAsync("/auth/login/2fa/resend", new SendLoginTwoFactorOtpRequest { Email = email });
        resend.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.EmailSender.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task LoginTwoFactorResend_ForNonexistentEmail_ReturnsOk_SilentSuccess()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();

        var resend = await client.PostAsJsonAsync("/auth/login/2fa/resend", new SendLoginTwoFactorOtpRequest
        {
            Email = $"nobody-{Guid.NewGuid():N}@example.com"
        });

        resend.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.EmailSender.Messages.Should().BeEmpty();
    }

    private async Task EnableTwoFactorAsync(HttpClient client, string email, string password)
    {
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

        client.DefaultRequestHeaders.Authorization = null;
        _factory.EmailSender.Clear();
    }

    private static string? ExtractCode(string html)
    {
        var match = Regex.Match(html, @">\s*(\d{6})\s*<");
        return match.Success ? match.Groups[1].Value : null;
    }
}
