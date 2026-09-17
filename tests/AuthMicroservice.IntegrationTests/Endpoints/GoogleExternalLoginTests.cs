using System.Net;
using System.Net.Http.Json;
using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Contracts.Responses;
using AuthMicroservice.Core.Services.Abstractions;
using AuthMicroservice.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AuthMicroservice.IntegrationTests.Endpoints;

public class GoogleExternalLoginTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public GoogleExternalLoginTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NewGoogleUser_IsAutoProvisioned_AndTokensReturned()
    {
        var client = _factory.CreateClient();
        var email = $"google-new-{Guid.NewGuid():N}@example.com";
        var idToken = $"fake-token-{Guid.NewGuid():N}";

        _factory.GoogleTokenValidator.RegisterToken(idToken, new GoogleUserInfo(
            Subject: $"google-sub-{Guid.NewGuid():N}",
            Email: email,
            EmailVerified: true,
            Name: "Google User",
            PictureUrl: "https://example.com/pic.png"));

        var response = await client.PostAsJsonAsync("/auth/external/google", new GoogleExternalLoginRequest { IdToken = idToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
        body.User.Email.Should().Be(email);
        body.User.FullName.Should().Be("Google User");
        body.User.EmailConfirmed.Should().BeTrue();
        body.User.Roles.Should().Contain("User");
    }

    [Fact]
    public async Task SecondCall_WithSameSubject_ReusesExistingUser()
    {
        var client = _factory.CreateClient();
        var email = $"google-reuse-{Guid.NewGuid():N}@example.com";
        var subject = $"google-sub-{Guid.NewGuid():N}";
        var idToken = $"fake-token-{Guid.NewGuid():N}";

        _factory.GoogleTokenValidator.RegisterToken(idToken, new GoogleUserInfo(
            Subject: subject, Email: email, EmailVerified: true, Name: "Google User", PictureUrl: null));

        var first = await client.PostAsJsonAsync("/auth/external/google", new GoogleExternalLoginRequest { IdToken = idToken });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<AuthResponse>();

        var second = await client.PostAsJsonAsync("/auth/external/google", new GoogleExternalLoginRequest { IdToken = idToken });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<AuthResponse>();

        secondBody!.User.Id.Should().Be(firstBody!.User.Id);
    }

    [Fact]
    public async Task EmailNotVerified_ByGoogle_Returns400()
    {
        var client = _factory.CreateClient();
        var idToken = $"fake-token-{Guid.NewGuid():N}";

        _factory.GoogleTokenValidator.RegisterToken(idToken, new GoogleUserInfo(
            Subject: $"sub-{Guid.NewGuid():N}",
            Email: $"unverified-{Guid.NewGuid():N}@example.com",
            EmailVerified: false,
            Name: null,
            PictureUrl: null));

        var response = await client.PostAsJsonAsync("/auth/external/google", new GoogleExternalLoginRequest { IdToken = idToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidIdToken_Returns401()
    {
        var client = _factory.CreateClient();
        var idToken = $"bad-token-{Guid.NewGuid():N}";

        _factory.GoogleTokenValidator.RegisterFailure(idToken, "signature verification failed");

        var response = await client.PostAsJsonAsync("/auth/external/google", new GoogleExternalLoginRequest { IdToken = idToken });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmptyIdToken_Returns400_Validation()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/external/google", new GoogleExternalLoginRequest { IdToken = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExistingLocalUser_WithVerifiedEmail_IsAutoLinked()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"link-verified-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!",
            FullName = "Local User"
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var registered = await register.Content.ReadFromJsonAsync<AuthResponse>();

        // In the test config RequireConfirmedEmail=false, but the register flow does NOT set
        // EmailConfirmed=true. Simulate email confirmation by calling /auth/verify-email with a
        // freshly generated token via UserManager (accessed through the API's DI).
        await ConfirmEmailAsync(email);

        var idToken = $"link-token-{Guid.NewGuid():N}";
        _factory.GoogleTokenValidator.RegisterToken(idToken, new GoogleUserInfo(
            Subject: $"google-{Guid.NewGuid():N}",
            Email: email,
            EmailVerified: true,
            Name: "Google Name",
            PictureUrl: null));

        var googleLogin = await client.PostAsJsonAsync("/auth/external/google", new GoogleExternalLoginRequest { IdToken = idToken });
        googleLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var linked = await googleLogin.Content.ReadFromJsonAsync<AuthResponse>();
        linked!.User.Id.Should().Be(registered!.User.Id);
    }

    [Fact]
    public async Task ExistingLocalUser_WithUnverifiedEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = $"link-unverified-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!",
            FullName = "Local User"
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var idToken = $"link-token-{Guid.NewGuid():N}";
        _factory.GoogleTokenValidator.RegisterToken(idToken, new GoogleUserInfo(
            Subject: $"google-{Guid.NewGuid():N}",
            Email: email,
            EmailVerified: true,
            Name: null,
            PictureUrl: null));

        var response = await client.PostAsJsonAsync("/auth/external/google", new GoogleExternalLoginRequest { IdToken = idToken });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task ConfirmEmailAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AuthMicroservice.Core.Domain.ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull();
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);
        var result = await userManager.ConfirmEmailAsync(user!, token);
        result.Succeeded.Should().BeTrue();
    }
}
