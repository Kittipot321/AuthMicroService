using System.Net;
using System.Net.Http.Json;
using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Contracts.Responses;
using AuthMicroservice.Core.Services.Abstractions;
using AuthMicroservice.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AuthMicroservice.IntegrationTests.Endpoints;

public class LineExternalLoginTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public LineExternalLoginTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NewLineUser_IsAutoProvisioned_AndTokensReturned()
    {
        var client = _factory.CreateClient();
        var email = $"line-new-{Guid.NewGuid():N}@example.com";
        var idToken = $"fake-token-{Guid.NewGuid():N}";

        _factory.LineTokenValidator.RegisterToken(idToken, new LineUserInfo(
            Subject: $"line-sub-{Guid.NewGuid():N}",
            Email: email,
            Name: "LINE User",
            PictureUrl: "https://example.com/pic.png"));

        var response = await client.PostAsJsonAsync("/auth/external/line", new LineExternalLoginRequest { IdToken = idToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
        body.User.Email.Should().Be(email);
        body.User.FullName.Should().Be("LINE User");
        body.User.EmailConfirmed.Should().BeTrue();
        body.User.Roles.Should().Contain("User");
    }

    [Fact]
    public async Task SecondCall_WithSameSubject_ReusesExistingUser()
    {
        var client = _factory.CreateClient();
        var email = $"line-reuse-{Guid.NewGuid():N}@example.com";
        var subject = $"line-sub-{Guid.NewGuid():N}";
        var idToken = $"fake-token-{Guid.NewGuid():N}";

        _factory.LineTokenValidator.RegisterToken(idToken, new LineUserInfo(
            Subject: subject, Email: email, Name: "LINE User", PictureUrl: null));

        var first = await client.PostAsJsonAsync("/auth/external/line", new LineExternalLoginRequest { IdToken = idToken });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<AuthResponse>();

        var second = await client.PostAsJsonAsync("/auth/external/line", new LineExternalLoginRequest { IdToken = idToken });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<AuthResponse>();

        secondBody!.User.Id.Should().Be(firstBody!.User.Id);
    }

    [Fact]
    public async Task MissingEmail_Returns400()
    {
        var client = _factory.CreateClient();
        var idToken = $"fake-token-{Guid.NewGuid():N}";

        _factory.LineTokenValidator.RegisterToken(idToken, new LineUserInfo(
            Subject: $"sub-{Guid.NewGuid():N}",
            Email: null,
            Name: "No Email User",
            PictureUrl: null));

        var response = await client.PostAsJsonAsync("/auth/external/line", new LineExternalLoginRequest { IdToken = idToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidIdToken_Returns401()
    {
        var client = _factory.CreateClient();
        var idToken = $"bad-token-{Guid.NewGuid():N}";

        _factory.LineTokenValidator.RegisterFailure(idToken, "signature verification failed");

        var response = await client.PostAsJsonAsync("/auth/external/line", new LineExternalLoginRequest { IdToken = idToken });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmptyIdToken_Returns400_Validation()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/external/line", new LineExternalLoginRequest { IdToken = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExistingLocalUser_WithVerifiedEmail_IsAutoLinked()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"line-link-verified-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!",
            FullName = "Local User"
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var registered = await register.Content.ReadFromJsonAsync<AuthResponse>();

        await ConfirmEmailAsync(email);

        var idToken = $"link-token-{Guid.NewGuid():N}";
        _factory.LineTokenValidator.RegisterToken(idToken, new LineUserInfo(
            Subject: $"line-{Guid.NewGuid():N}",
            Email: email,
            Name: "LINE Name",
            PictureUrl: null));

        var lineLogin = await client.PostAsJsonAsync("/auth/external/line", new LineExternalLoginRequest { IdToken = idToken });
        lineLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var linked = await lineLogin.Content.ReadFromJsonAsync<AuthResponse>();
        linked!.User.Id.Should().Be(registered!.User.Id);
    }

    [Fact]
    public async Task ExistingLocalUser_WithUnverifiedEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = $"line-link-unverified-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!",
            FullName = "Local User"
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var idToken = $"link-token-{Guid.NewGuid():N}";
        _factory.LineTokenValidator.RegisterToken(idToken, new LineUserInfo(
            Subject: $"line-{Guid.NewGuid():N}",
            Email: email,
            Name: null,
            PictureUrl: null));

        var response = await client.PostAsJsonAsync("/auth/external/line", new LineExternalLoginRequest { IdToken = idToken });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeactivatedUser_Returns403()
    {
        var client = _factory.CreateClient();
        var email = $"line-deactivated-{Guid.NewGuid():N}@example.com";
        var subject = $"line-sub-{Guid.NewGuid():N}";
        var idToken = $"fake-token-{Guid.NewGuid():N}";

        _factory.LineTokenValidator.RegisterToken(idToken, new LineUserInfo(
            Subject: subject, Email: email, Name: "LINE User", PictureUrl: null));

        var first = await client.PostAsJsonAsync("/auth/external/line", new LineExternalLoginRequest { IdToken = idToken });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        await DeactivateUserAsync(email);

        var second = await client.PostAsJsonAsync("/auth/external/line", new LineExternalLoginRequest { IdToken = idToken });
        second.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    private async Task DeactivateUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AuthMicroservice.Core.Domain.ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull();
        user!.IsDeactivated = true;
        var result = await userManager.UpdateAsync(user);
        result.Succeeded.Should().BeTrue();
    }
}
