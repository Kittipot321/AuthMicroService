using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthMicroservice.Core.Contracts.Common;
using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Contracts.Responses;
using AuthMicroservice.IntegrationTests.Infrastructure;
using FluentAssertions;
using OtpNet;

namespace AuthMicroservice.IntegrationTests.Endpoints;

public class TotpEndpointsTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public TotpEndpointsTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TotpSetup_ReturnsSecretUriAndQrCode()
    {
        var (client, _, _) = await RegisterAndSignInAsync();

        var setup = await client.PostAsync("/auth/2fa/totp/setup", content: null);
        setup.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await setup.Content.ReadFromJsonAsync<TotpSetupResponse>();
        body!.SecretBase32.Should().NotBeNullOrWhiteSpace();
        body.OtpauthUri.Should().StartWith("otpauth://totp/");
        body.QrCodePngBase64.Should().NotBeNullOrWhiteSpace();
        body.Digits.Should().Be(6);
        body.PeriodSeconds.Should().Be(30);
    }

    [Fact]
    public async Task TotpEnableConfirm_WithValidCode_EnablesAndReturnsRecoveryCodes()
    {
        var (client, email, _) = await RegisterAndSignInAsync();

        var setup = await client.PostAsync("/auth/2fa/totp/setup", content: null);
        var setupBody = await setup.Content.ReadFromJsonAsync<TotpSetupResponse>();
        var code = new Totp(Base32Encoding.ToBytes(setupBody!.SecretBase32)).ComputeTotp();

        var confirm = await client.PostAsJsonAsync("/auth/2fa/totp/enable-confirm", new TotpEnableConfirmRequest { Code = code });
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await confirm.Content.ReadFromJsonAsync<TotpEnableConfirmResponse>();
        body!.RecoveryCodes.Should().HaveCount(10);
        body.RecoveryCodes.Distinct().Should().HaveCount(10);
    }

    [Fact]
    public async Task TotpEnableConfirm_WithBadCode_ReturnsUnauthorized()
    {
        var (client, _, _) = await RegisterAndSignInAsync();

        await client.PostAsync("/auth/2fa/totp/setup", content: null);
        var confirm = await client.PostAsJsonAsync("/auth/2fa/totp/enable-confirm", new TotpEnableConfirmRequest { Code = "000000" });

        confirm.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithTotpEnabled_Returns202_WithTotpMethod_AndNoEmailSent()
    {
        var (client, email, password) = await RegisterAndSignInAsync();

        var setup = await client.PostAsync("/auth/2fa/totp/setup", content: null);
        var setupBody = await setup.Content.ReadFromJsonAsync<TotpSetupResponse>();
        var setupCode = new Totp(Base32Encoding.ToBytes(setupBody!.SecretBase32)).ComputeTotp();
        await client.PostAsJsonAsync("/auth/2fa/totp/enable-confirm", new TotpEnableConfirmRequest { Code = setupCode });

        client.DefaultRequestHeaders.Authorization = null;
        _factory.EmailSender.Clear();

        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = password });
        login.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var challenge = await login.Content.ReadFromJsonAsync<TwoFactorRequiredResponse>();
        challenge!.Methods.Should().Contain(TwoFactorMethodNames.Totp);
        challenge.Methods.Should().NotContain(TwoFactorMethodNames.Email);
        challenge.EmailChallengeSent.Should().BeFalse();
        _factory.EmailSender.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task LoginTotpVerify_WithValidCode_ReturnsTokens()
    {
        var (client, email, password) = await RegisterAndSignInAsync();

        var setup = await client.PostAsync("/auth/2fa/totp/setup", content: null);
        var setupBody = await setup.Content.ReadFromJsonAsync<TotpSetupResponse>();
        var secret = setupBody!.SecretBase32;
        var setupCode = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();
        await client.PostAsJsonAsync("/auth/2fa/totp/enable-confirm", new TotpEnableConfirmRequest { Code = setupCode });

        client.DefaultRequestHeaders.Authorization = null;
        await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = password });

        var loginCode = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();
        var verify = await client.PostAsJsonAsync("/auth/login/2fa/totp/verify", new LoginTotpVerifyRequest
        {
            Email = email,
            Code = loginCode
        });

        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await verify.Content.ReadFromJsonAsync<AuthResponse>();
        tokens!.AccessToken.Should().NotBeNullOrEmpty();
        tokens.User.TwoFactorEnabled.Should().BeTrue();
        tokens.User.TwoFactorMethods.Should().Contain(TwoFactorMethodNames.Totp);
    }

    [Fact]
    public async Task LoginTotpVerify_WithBadCode_ReturnsUnauthorized()
    {
        var (client, email, password) = await RegisterAndSignInAsync();

        var setup = await client.PostAsync("/auth/2fa/totp/setup", content: null);
        var setupBody = await setup.Content.ReadFromJsonAsync<TotpSetupResponse>();
        var setupCode = new Totp(Base32Encoding.ToBytes(setupBody!.SecretBase32)).ComputeTotp();
        await client.PostAsJsonAsync("/auth/2fa/totp/enable-confirm", new TotpEnableConfirmRequest { Code = setupCode });

        client.DefaultRequestHeaders.Authorization = null;
        await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = password });

        var verify = await client.PostAsJsonAsync("/auth/login/2fa/totp/verify", new LoginTotpVerifyRequest
        {
            Email = email,
            Code = "000000"
        });
        verify.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TotpDisable_WithCorrectPassword_ClearsFlag()
    {
        var (client, email, password) = await RegisterAndSignInAsync();

        var setup = await client.PostAsync("/auth/2fa/totp/setup", content: null);
        var setupBody = await setup.Content.ReadFromJsonAsync<TotpSetupResponse>();
        var setupCode = new Totp(Base32Encoding.ToBytes(setupBody!.SecretBase32)).ComputeTotp();
        await client.PostAsJsonAsync("/auth/2fa/totp/enable-confirm", new TotpEnableConfirmRequest { Code = setupCode });

        var disable = await client.PostAsJsonAsync("/auth/2fa/totp/disable", new TotpDisableRequest { Password = password });
        disable.StatusCode.Should().Be(HttpStatusCode.NoContent);

        client.DefaultRequestHeaders.Authorization = null;
        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TotpDisable_WithWrongPassword_ReturnsUnauthorized()
    {
        var (client, _, _) = await RegisterAndSignInAsync();

        var setup = await client.PostAsync("/auth/2fa/totp/setup", content: null);
        var setupBody = await setup.Content.ReadFromJsonAsync<TotpSetupResponse>();
        var setupCode = new Totp(Base32Encoding.ToBytes(setupBody!.SecretBase32)).ComputeTotp();
        await client.PostAsJsonAsync("/auth/2fa/totp/enable-confirm", new TotpEnableConfirmRequest { Code = setupCode });

        var disable = await client.PostAsJsonAsync("/auth/2fa/totp/disable", new TotpDisableRequest { Password = "wrong" });
        disable.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TotpSetup_WhenAlreadyEnabled_ReturnsConflict()
    {
        var (client, _, _) = await RegisterAndSignInAsync();

        var setup = await client.PostAsync("/auth/2fa/totp/setup", content: null);
        var setupBody = await setup.Content.ReadFromJsonAsync<TotpSetupResponse>();
        var setupCode = new Totp(Base32Encoding.ToBytes(setupBody!.SecretBase32)).ComputeTotp();
        await client.PostAsJsonAsync("/auth/2fa/totp/enable-confirm", new TotpEnableConfirmRequest { Code = setupCode });

        var second = await client.PostAsync("/auth/2fa/totp/setup", content: null);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<(HttpClient client, string email, string password)> RegisterAndSignInAsync()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"totp-{Guid.NewGuid():N}@example.com";
        const string password = "P@ssw0rd!";

        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password
        });
        var tokens = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return (client, email, password);
    }
}
