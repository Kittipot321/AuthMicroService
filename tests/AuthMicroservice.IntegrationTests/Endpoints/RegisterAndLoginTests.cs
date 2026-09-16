using System.Net;
using System.Net.Http.Json;
using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Contracts.Responses;
using AuthMicroservice.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace AuthMicroservice.IntegrationTests.Endpoints;

public class RegisterAndLoginTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public RegisterAndLoginTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ThenLogin_ReturnsTokens()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!",
            FullName = "Test User"
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var registerBody = await register.Content.ReadFromJsonAsync<AuthResponse>();
        registerBody!.AccessToken.Should().NotBeNullOrEmpty();
        registerBody.RefreshToken.Should().NotBeNullOrEmpty();

        // Verification email was captured.
        _factory.EmailSender.Messages.Should().ContainSingle(m => m.To == email);

        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = "P@ssw0rd!" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await login.Content.ReadFromJsonAsync<AuthResponse>();
        loginBody!.AccessToken.Should().NotBeNullOrEmpty();
        loginBody.User.Email.Should().Be(email);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!",
            FullName = "Test User"
        });

        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = "wrong-password" });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithLockout_ReturnsLocked()
    {
        var client = _factory.CreateClient();
        var email = $"lockout-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!",
            FullName = "Lockout User"
        });

        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = "wrong" });
        }

        var final = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = "P@ssw0rd!" });
        final.StatusCode.Should().Be(HttpStatusCode.Locked);
    }
}
