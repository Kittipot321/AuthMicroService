using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Contracts.Responses;
using AuthMicroservice.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace AuthMicroservice.IntegrationTests.Endpoints;

public class RefreshFlowTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public RefreshFlowTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Refresh_RotatesTokens_And_OldRefresh_IsRejected()
    {
        var client = _factory.CreateClient();
        var email = $"refresh-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!",
            FullName = "Refresh User"
        });
        var initial = await register.Content.ReadFromJsonAsync<AuthResponse>();

        var refresh1 = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest
        {
            AccessToken = initial!.AccessToken,
            RefreshToken = initial.RefreshToken
        });
        refresh1.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await refresh1.Content.ReadFromJsonAsync<AuthResponse>();
        rotated!.RefreshToken.Should().NotBe(initial.RefreshToken);

        // Old refresh must not work again
        var replay = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest
        {
            AccessToken = initial.AccessToken,
            RefreshToken = initial.RefreshToken
        });
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_Returns401_WithoutToken_And_Returns200_WithToken()
    {
        var client = _factory.CreateClient();
        var email = $"me-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!"
        });
        var body = await register.Content.ReadFromJsonAsync<AuthResponse>();

        var unauth = await client.GetAsync("/auth/me");
        unauth.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        var authed = await client.GetAsync("/auth/me");
        authed.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await authed.Content.ReadFromJsonAsync<UserResponse>();
        user!.Email.Should().Be(email);
        user.Roles.Should().Contain("User");
    }
}
