using System.Net;
using System.Net.Http.Json;
using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Contracts.Responses;
using AuthMicroservice.Core.Services.Abstractions;
using AuthMicroservice.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AuthMicroservice.IntegrationTests.Endpoints;

public class MicrosoftExternalLoginTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public MicrosoftExternalLoginTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NewMicrosoftUser_IsAutoProvisioned_AndTokensReturned()
    {
        var client = _factory.CreateClient();
        var email = $"ms-new-{Guid.NewGuid():N}@example.com";
        var idToken = $"fake-token-{Guid.NewGuid():N}";

        _factory.MicrosoftTokenValidator.RegisterToken(idToken, new MicrosoftUserInfo(
            Subject: $"ms-sub-{Guid.NewGuid():N}",
            Email: email,
            Name: "Microsoft User",
            TenantId: "9188040d-6c67-4c5b-b112-36a304b66dad"));

        var response = await client.PostAsJsonAsync("/auth/external/microsoft", new MicrosoftExternalLoginRequest { IdToken = idToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
        body.User.Email.Should().Be(email);
        body.User.FullName.Should().Be("Microsoft User");
        body.User.EmailConfirmed.Should().BeTrue();
        body.User.Roles.Should().Contain("User");
    }

    [Fact]
    public async Task SecondCall_WithSameSubject_ReusesExistingUser()
    {
        var client = _factory.CreateClient();
        var email = $"ms-reuse-{Guid.NewGuid():N}@example.com";
        var subject = $"ms-sub-{Guid.NewGuid():N}";
        var idToken = $"fake-token-{Guid.NewGuid():N}";

        _factory.MicrosoftTokenValidator.RegisterToken(idToken, new MicrosoftUserInfo(
            Subject: subject, Email: email, Name: "Microsoft User", TenantId: null));

        var first = await client.PostAsJsonAsync("/auth/external/microsoft", new MicrosoftExternalLoginRequest { IdToken = idToken });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<AuthResponse>();

        var second = await client.PostAsJsonAsync("/auth/external/microsoft", new MicrosoftExternalLoginRequest { IdToken = idToken });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<AuthResponse>();

        secondBody!.User.Id.Should().Be(firstBody!.User.Id);
    }

    [Fact]
    public async Task InvalidIdToken_Returns401()
    {
        var client = _factory.CreateClient();
        var idToken = $"bad-token-{Guid.NewGuid():N}";

        _factory.MicrosoftTokenValidator.RegisterFailure(idToken, "signature verification failed");

        var response = await client.PostAsJsonAsync("/auth/external/microsoft", new MicrosoftExternalLoginRequest { IdToken = idToken });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmptyIdToken_Returns400_Validation()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/external/microsoft", new MicrosoftExternalLoginRequest { IdToken = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExistingLocalUser_WithVerifiedEmail_IsAutoLinked()
    {
        _factory.EmailSender.Clear();
        var client = _factory.CreateClient();
        var email = $"ms-link-verified-{Guid.NewGuid():N}@example.com";

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
        _factory.MicrosoftTokenValidator.RegisterToken(idToken, new MicrosoftUserInfo(
            Subject: $"ms-{Guid.NewGuid():N}",
            Email: email,
            Name: "Microsoft Name",
            TenantId: null));

        var msLogin = await client.PostAsJsonAsync("/auth/external/microsoft", new MicrosoftExternalLoginRequest { IdToken = idToken });
        msLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var linked = await msLogin.Content.ReadFromJsonAsync<AuthResponse>();
        linked!.User.Id.Should().Be(registered!.User.Id);
    }

    [Fact]
    public async Task ExistingLocalUser_WithUnverifiedEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = $"ms-link-unverified-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd!",
            FullName = "Local User"
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var idToken = $"link-token-{Guid.NewGuid():N}";
        _factory.MicrosoftTokenValidator.RegisterToken(idToken, new MicrosoftUserInfo(
            Subject: $"ms-{Guid.NewGuid():N}",
            Email: email,
            Name: null,
            TenantId: null));

        var response = await client.PostAsJsonAsync("/auth/external/microsoft", new MicrosoftExternalLoginRequest { IdToken = idToken });

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
