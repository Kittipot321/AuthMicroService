using AuthMicroservice.Core.Configuration;
using FluentAssertions;

namespace AuthMicroservice.UnitTests.Configuration;

public class AuthMicroserviceOptionsValidatorTests
{
    private static AuthMicroserviceOptions ValidBaseline() => new()
    {
        Jwt = new JwtOptions
        {
            Key = new string('k', 32),
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeDays = 7
        },
        TokenLinks = new TokenLinkOptions
        {
            PasswordResetBaseUrl = "https://example.com/reset"
        },
        EmailVerification = new EmailVerificationOptions
        {
            Mode = EmailVerificationMode.Link,
            LinkBaseUrl = "https://example.com/verify"
        }
    };

    [Fact]
    public void Baseline_IsValid()
    {
        var validator = new AuthMicroserviceOptionsValidator();
        var result = validator.Validate(null, ValidBaseline());
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Google_Enabled_Without_ClientId_Fails()
    {
        var options = ValidBaseline();
        options.ExternalProviders.Google.Enabled = true;
        options.ExternalProviders.Google.ClientId = string.Empty;

        var validator = new AuthMicroserviceOptionsValidator();
        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("ExternalProviders:Google:ClientId"));
    }

    [Fact]
    public void Google_Enabled_With_ClientId_Passes()
    {
        var options = ValidBaseline();
        options.ExternalProviders.Google.Enabled = true;
        options.ExternalProviders.Google.ClientId = "my-client-id.apps.googleusercontent.com";

        var validator = new AuthMicroserviceOptionsValidator();
        var result = validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Google_Disabled_Ignores_ClientId()
    {
        var options = ValidBaseline();
        options.ExternalProviders.Google.Enabled = false;
        options.ExternalProviders.Google.ClientId = string.Empty;

        var validator = new AuthMicroserviceOptionsValidator();
        var result = validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void EmailVerification_LinkMode_Without_LinkBaseUrl_Fails()
    {
        var options = ValidBaseline();
        options.EmailVerification.Mode = EmailVerificationMode.Link;
        options.EmailVerification.LinkBaseUrl = string.Empty;

        var validator = new AuthMicroserviceOptionsValidator();
        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("EmailVerification:LinkBaseUrl"));
    }

    [Fact]
    public void EmailVerification_CodeMode_Ignores_LinkBaseUrl()
    {
        var options = ValidBaseline();
        options.EmailVerification.Mode = EmailVerificationMode.Code;
        options.EmailVerification.LinkBaseUrl = string.Empty;

        var validator = new AuthMicroserviceOptionsValidator();
        var result = validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void EmailVerification_ModeDisabled_Passes()
    {
        var options = ValidBaseline();
        options.EmailVerification.Mode = EmailVerificationMode.Disabled;

        var validator = new AuthMicroserviceOptionsValidator();
        var result = validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }
}
