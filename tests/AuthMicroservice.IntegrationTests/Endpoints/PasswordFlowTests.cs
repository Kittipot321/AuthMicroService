using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Contracts.Responses;
using AuthMicroservice.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace AuthMicroservice.IntegrationTests.Endpoints;

public class PasswordFlowTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public PasswordFlowTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ForgotPassword_SendsEmailWithToken_AndResetSucceeds()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"reset-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!"
        });
        _factory.EmailSender.Clear(); // ignore verification email

        var forgot = await client.PostAsJsonAsync("/auth/forgot-password", new ForgotPasswordRequest { Email = email });
        forgot.StatusCode.Should().Be(HttpStatusCode.OK);

        var msg = _factory.EmailSender.Messages.Should().ContainSingle().Subject;
        var token = ExtractToken(msg.HtmlBody, "token");
        token.Should().NotBeNullOrEmpty();

        var reset = await client.PostAsJsonAsync("/auth/reset-password", new ResetPasswordRequest
        {
            Email = email,
            Token = token!,
            NewPassword = "N3wP@ssw0rd!"
        });
        reset.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = "N3wP@ssw0rd!" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_RevokesExistingRefreshTokens()
    {
        var client = _factory.CreateClient();
        var email = $"change-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!"
        });
        var tokens = await register.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var change = await client.PostAsJsonAsync("/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "P@ssw0rd!",
            NewPassword = "N3wP@ssw0rd!"
        });
        change.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Previously-issued refresh token should no longer work
        client.DefaultRequestHeaders.Authorization = null;
        var refresh = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken
        });
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string? ExtractToken(string body, string paramName)
    {
        var match = Regex.Match(body, $@"{paramName}=([^""'&\s<>]+)");
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
    }
}
