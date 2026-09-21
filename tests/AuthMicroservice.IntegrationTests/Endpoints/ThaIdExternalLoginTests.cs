using System.Net;
using System.Net.Http.Json;
using System.Web;
using AuthMicroservice.Core.Contracts.Responses;
using AuthMicroservice.Core.Services.Abstractions;
using AuthMicroservice.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AuthMicroservice.IntegrationTests.Endpoints;

public class ThaIdExternalLoginTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public ThaIdExternalLoginTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Challenge_ReturnsRedirect_WithStateAndCodeChallenge()
    {
        var client = CreateNoRedirectClient();
        var returnUrl = "https://app.test.local/callback";

        var response = await client.GetAsync("/auth/external/thaid/challenge?returnUrl=" + Uri.EscapeDataString(returnUrl));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith("https://fake-thaid.local/authorize?");
        var query = HttpUtility.ParseQueryString(new Uri(location).Query);
        query.Get("state").Should().NotBeNullOrEmpty();
        query.Get("code_challenge").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Challenge_ReturnUrlOutsideWhitelist_Returns400()
    {
        var client = CreateNoRedirectClient();

        var response = await client.GetAsync(
            "/auth/external/thaid/challenge?returnUrl=" + Uri.EscapeDataString("https://evil.example.com/steal"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Callback_ValidCodeAndState_ProvisionsUser_AndRedirectsWithTokens()
    {
        _factory.ThaIdOidcClient.Clear();
        var client = CreateNoRedirectClient();
        var returnUrl = "https://app.test.local/home";
        var pid = NewPid();
        var email = $"thaid-{Guid.NewGuid():N}@example.com";
        var code = $"code-{Guid.NewGuid():N}";

        _factory.ThaIdOidcClient.RegisterCode(code, new ThaIdUserInfo(
            Pid: pid,
            Email: email,
            GivenName: "สมชาย",
            FamilyName: "ทดสอบ",
            Birthdate: "1990-01-01",
            Address: "Bangkok"));

        var state = await StartChallengeAsync(client, returnUrl);

        var callback = await client.GetAsync($"/auth/external/thaid/callback?code={code}&state={state}");

        callback.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = callback.Headers.Location!.ToString();
        location.Should().StartWith(returnUrl + "#");
        var fragment = HttpUtility.ParseQueryString(location.Substring(location.IndexOf('#') + 1));
        fragment.Get("access_token").Should().NotBeNullOrEmpty();
        fragment.Get("refresh_token").Should().NotBeNullOrEmpty();
        fragment.Get("expires_at").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Callback_NoReturnUrl_ReturnsJson()
    {
        _factory.ThaIdOidcClient.Clear();
        var client = CreateNoRedirectClient();
        var pid = NewPid();
        var code = $"code-{Guid.NewGuid():N}";

        _factory.ThaIdOidcClient.RegisterCode(code, new ThaIdUserInfo(
            Pid: pid,
            Email: null,
            GivenName: "No",
            FamilyName: "Return",
            Birthdate: null,
            Address: null));

        var state = await StartChallengeAsync(client, returnUrl: null);

        var callback = await client.GetAsync($"/auth/external/thaid/callback?code={code}&state={state}");

        callback.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await callback.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Callback_UnknownState_Returns400()
    {
        var client = CreateNoRedirectClient();

        var response = await client.GetAsync(
            $"/auth/external/thaid/callback?code=any-code&state=unknown-state-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Callback_CodeExchangeFails_Returns401()
    {
        _factory.ThaIdOidcClient.Clear();
        var client = CreateNoRedirectClient();
        var badCode = $"bad-{Guid.NewGuid():N}";
        _factory.ThaIdOidcClient.RegisterFailure(badCode, "invalid_grant");

        var state = await StartChallengeAsync(client, returnUrl: "https://app.test.local/x");

        var response = await client.GetAsync($"/auth/external/thaid/callback?code={badCode}&state={state}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Callback_ProviderReturnsError_Returns401()
    {
        var client = CreateNoRedirectClient();

        var response = await client.GetAsync(
            "/auth/external/thaid/callback?code=&state=&error=access_denied&error_description=User+cancelled");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SecondCallback_WithSamePid_ReusesUser()
    {
        _factory.ThaIdOidcClient.Clear();
        var client = CreateNoRedirectClient();
        var pid = NewPid();
        var email = $"thaid-reuse-{Guid.NewGuid():N}@example.com";
        var user = new ThaIdUserInfo(pid, email, "A", "B", null, null);

        var code1 = $"code1-{Guid.NewGuid():N}";
        var code2 = $"code2-{Guid.NewGuid():N}";
        _factory.ThaIdOidcClient.RegisterCode(code1, user);
        _factory.ThaIdOidcClient.RegisterCode(code2, user);

        var state1 = await StartChallengeAsync(client, returnUrl: null);
        var first = await client.GetAsync($"/auth/external/thaid/callback?code={code1}&state={state1}");
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<AuthResponse>();

        var state2 = await StartChallengeAsync(client, returnUrl: null);
        var second = await client.GetAsync($"/auth/external/thaid/callback?code={code2}&state={state2}");
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<AuthResponse>();

        secondBody!.User.Id.Should().Be(firstBody!.User.Id);
    }

    [Fact]
    public async Task ThaIdUser_WithoutEmail_IsProvisioned()
    {
        _factory.ThaIdOidcClient.Clear();
        var client = CreateNoRedirectClient();
        var pid = NewPid();
        var code = $"code-{Guid.NewGuid():N}";

        _factory.ThaIdOidcClient.RegisterCode(code, new ThaIdUserInfo(
            Pid: pid,
            Email: null,
            GivenName: "Anon",
            FamilyName: "User",
            Birthdate: null,
            Address: null));

        var state = await StartChallengeAsync(client, returnUrl: null);
        var response = await client.GetAsync($"/auth/external/thaid/callback?code={code}&state={state}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.User.EmailConfirmed.Should().BeFalse();
        body.User.FullName.Should().Be("Anon User");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AuthMicroservice.Core.Domain.ApplicationUser>>();
        var appUser = await userManager.FindByLoginAsync("ThaId", pid);
        appUser.Should().NotBeNull();
        appUser!.Email.Should().Be($"{pid}@thaid.local");
        appUser.EmailConfirmed.Should().BeFalse();
        appUser.UserName.Should().Be("thaid." + pid);
    }

    private HttpClient CreateNoRedirectClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private static async Task<string> StartChallengeAsync(HttpClient client, string? returnUrl)
    {
        var url = "/auth/external/thaid/challenge";
        if (!string.IsNullOrEmpty(returnUrl))
        {
            url += "?returnUrl=" + Uri.EscapeDataString(returnUrl);
        }

        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        var query = HttpUtility.ParseQueryString(new Uri(location).Query);
        var state = query.Get("state");
        state.Should().NotBeNullOrEmpty();
        return state!;
    }

    private static string NewPid()
    {
        var pid = new char[13];
        var rand = new Random();
        for (var i = 0; i < 13; i++)
        {
            pid[i] = (char)('0' + rand.Next(0, 10));
        }
        return new string(pid);
    }
}
