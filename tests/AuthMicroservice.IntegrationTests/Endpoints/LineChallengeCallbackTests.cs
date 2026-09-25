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

public class LineChallengeCallbackTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public LineChallengeCallbackTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Challenge_ReturnsRedirect_WithStateCodeChallengeAndNonce()
    {
        var client = CreateNoRedirectClient();
        var returnUrl = "https://app.test.local/callback";

        var response = await client.GetAsync("/auth/challenge/line?returnUrl=" + Uri.EscapeDataString(returnUrl));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith("https://fake-line.local/authorize?");
        var query = HttpUtility.ParseQueryString(new Uri(location).Query);
        query.Get("state").Should().NotBeNullOrEmpty();
        query.Get("code_challenge").Should().NotBeNullOrEmpty();
        query.Get("nonce").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Challenge_ReturnUrlOutsideWhitelist_Returns400()
    {
        var client = CreateNoRedirectClient();

        var response = await client.GetAsync(
            "/auth/challenge/line?returnUrl=" + Uri.EscapeDataString("https://evil.example.com/steal"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Callback_ValidCodeAndState_ProvisionsUser_AndRedirectsWithTokens()
    {
        _factory.LineOidcClient.Clear();
        var client = CreateNoRedirectClient();
        var returnUrl = "https://app.test.local/home";
        var subject = NewSubject();
        var email = $"line-{Guid.NewGuid():N}@example.com";
        var code = $"code-{Guid.NewGuid():N}";

        _factory.LineOidcClient.RegisterCode(code, new LineUserInfo(
            Subject: subject,
            Email: email,
            Name: "LINE Tester",
            PictureUrl: null));

        var state = await StartChallengeAsync(client, returnUrl);

        var callback = await client.GetAsync($"/auth/callback/line?code={code}&state={state}");

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
        _factory.LineOidcClient.Clear();
        var client = CreateNoRedirectClient();
        var subject = NewSubject();
        var code = $"code-{Guid.NewGuid():N}";

        _factory.LineOidcClient.RegisterCode(code, new LineUserInfo(
            Subject: subject,
            Email: null,
            Name: "No Return",
            PictureUrl: null));

        var state = await StartChallengeAsync(client, returnUrl: null);

        var callback = await client.GetAsync($"/auth/callback/line?code={code}&state={state}");

        callback.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await callback.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Callback_NonceMismatch_Returns401()
    {
        _factory.LineOidcClient.Clear();
        var client = CreateNoRedirectClient();
        var code = $"code-{Guid.NewGuid():N}";

        // Register a user with an EXPLICIT nonce that won't match whatever the challenge generates.
        _factory.LineOidcClient.RegisterCode(code, new LineUserInfo(
            Subject: NewSubject(),
            Email: null,
            Name: "Mismatch",
            PictureUrl: null,
            Nonce: "definitely-not-the-nonce"));

        var state = await StartChallengeAsync(client, returnUrl: null);

        var callback = await client.GetAsync($"/auth/callback/line?code={code}&state={state}");

        callback.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Callback_UnknownState_Returns400()
    {
        var client = CreateNoRedirectClient();

        var response = await client.GetAsync(
            $"/auth/callback/line?code=any-code&state=unknown-state-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Callback_StateAlreadyConsumed_Returns400()
    {
        _factory.LineOidcClient.Clear();
        var client = CreateNoRedirectClient();
        var subject = NewSubject();
        var code = $"code-{Guid.NewGuid():N}";

        _factory.LineOidcClient.RegisterCode(code, new LineUserInfo(
            Subject: subject, Email: null, Name: "One-shot", PictureUrl: null));

        var state = await StartChallengeAsync(client, returnUrl: null);

        var first = await client.GetAsync($"/auth/callback/line?code={code}&state={state}");
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var replay = await client.GetAsync($"/auth/callback/line?code={code}&state={state}");
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Callback_CodeExchangeFails_Returns401()
    {
        _factory.LineOidcClient.Clear();
        var client = CreateNoRedirectClient();
        var badCode = $"bad-{Guid.NewGuid():N}";
        _factory.LineOidcClient.RegisterFailure(badCode, "invalid_grant");

        var state = await StartChallengeAsync(client, returnUrl: "https://app.test.local/x");

        var response = await client.GetAsync($"/auth/callback/line?code={badCode}&state={state}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Callback_ProviderReturnsError_Returns401()
    {
        var client = CreateNoRedirectClient();

        var response = await client.GetAsync(
            "/auth/callback/line?code=&state=&error=access_denied&error_description=User+cancelled");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LineUser_WithoutEmail_IsProvisionedWithPlaceholder()
    {
        _factory.LineOidcClient.Clear();
        var client = CreateNoRedirectClient();
        var subject = NewSubject();
        var code = $"code-{Guid.NewGuid():N}";

        _factory.LineOidcClient.RegisterCode(code, new LineUserInfo(
            Subject: subject,
            Email: null,
            Name: "Anon LINE",
            PictureUrl: null));

        var state = await StartChallengeAsync(client, returnUrl: null);
        var response = await client.GetAsync($"/auth/callback/line?code={code}&state={state}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.User.EmailConfirmed.Should().BeFalse();
        body.User.FullName.Should().Be("Anon LINE");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AuthMicroservice.Core.Domain.ApplicationUser>>();
        var appUser = await userManager.FindByLoginAsync("Line", subject);
        appUser.Should().NotBeNull();
        appUser!.Email.Should().Be($"{subject}@line.local");
        appUser.UserName.Should().Be("line." + subject);
    }

    private HttpClient CreateNoRedirectClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private static async Task<string> StartChallengeAsync(HttpClient client, string? returnUrl)
    {
        var url = "/auth/challenge/line";
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

    private static string NewSubject() => "U" + Guid.NewGuid().ToString("N")[..16];
}

