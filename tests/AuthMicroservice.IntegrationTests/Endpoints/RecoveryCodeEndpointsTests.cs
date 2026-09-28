using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Contracts.Responses;
using AuthMicroservice.IntegrationTests.Infrastructure;
using FluentAssertions;
using OtpNet;

namespace AuthMicroservice.IntegrationTests.Endpoints;

public class RecoveryCodeEndpointsTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public RecoveryCodeEndpointsTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LoginRecoveryVerify_WithFreshCode_ReturnsTokens_AndConsumesCode()
    {
        var (client, email, password, codes) = await EnrollTotpAndGetRecoveryCodesAsync();
        var codeToUse = codes[0];

        client.DefaultRequestHeaders.Authorization = null;
        await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = password });

        var verify = await client.PostAsJsonAsync("/auth/login/2fa/recovery/verify", new LoginRecoveryCodeVerifyRequest
        {
            Email = email,
            Code = codeToUse
        });

        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await verify.Content.ReadFromJsonAsync<AuthResponse>();
        tokens!.AccessToken.Should().NotBeNullOrEmpty();

        var replay = await client.PostAsJsonAsync("/auth/login/2fa/recovery/verify", new LoginRecoveryCodeVerifyRequest
        {
            Email = email,
            Code = codeToUse
        });
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginRecoveryVerify_WithUnknownCode_ReturnsUnauthorized()
    {
        var (client, email, password, _) = await EnrollTotpAndGetRecoveryCodesAsync();

        client.DefaultRequestHeaders.Authorization = null;
        await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = password });

        var verify = await client.PostAsJsonAsync("/auth/login/2fa/recovery/verify", new LoginRecoveryCodeVerifyRequest
        {
            Email = email,
            Code = "AAAA-BBBB"
        });
        verify.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GenerateRecoveryCodes_WithCorrectPassword_ReturnsFreshSet_InvalidatingOldOnes()
    {
        var (client, email, password, oldCodes) = await EnrollTotpAndGetRecoveryCodesAsync();

        var response = await client.PostAsJsonAsync("/auth/2fa/recovery-codes/generate", new GenerateRecoveryCodesRequest { Password = password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RecoveryCodesResponse>();
        body!.Codes.Should().HaveCount(10);
        body.Codes.Intersect(oldCodes).Should().BeEmpty();

        client.DefaultRequestHeaders.Authorization = null;
        await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = password });

        var oldCodeTry = await client.PostAsJsonAsync("/auth/login/2fa/recovery/verify", new LoginRecoveryCodeVerifyRequest
        {
            Email = email,
            Code = oldCodes[0]
        });
        oldCodeTry.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GenerateRecoveryCodes_WithWrongPassword_ReturnsUnauthorized()
    {
        var (client, _, _, _) = await EnrollTotpAndGetRecoveryCodesAsync();

        var response = await client.PostAsJsonAsync("/auth/2fa/recovery-codes/generate", new GenerateRecoveryCodesRequest { Password = "wrong" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(HttpClient client, string email, string password, List<string> recoveryCodes)>
        EnrollTotpAndGetRecoveryCodesAsync()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"recov-{Guid.NewGuid():N}@example.com";
        const string password = "P@ssw0rd!";

        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password
        });
        var tokens = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var setup = await client.PostAsync("/auth/2fa/totp/setup", content: null);
        var setupBody = await setup.Content.ReadFromJsonAsync<TotpSetupResponse>();
        var code = new Totp(Base32Encoding.ToBytes(setupBody!.SecretBase32)).ComputeTotp();

        var enable = await client.PostAsJsonAsync("/auth/2fa/totp/enable-confirm", new TotpEnableConfirmRequest { Code = code });
        var enableBody = await enable.Content.ReadFromJsonAsync<TotpEnableConfirmResponse>();

        return (client, email, password, enableBody!.RecoveryCodes.ToList());
    }
}
