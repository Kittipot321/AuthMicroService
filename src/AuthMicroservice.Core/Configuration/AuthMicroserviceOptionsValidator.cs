using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Configuration;

internal sealed class AuthMicroserviceOptionsValidator : IValidateOptions<AuthMicroserviceOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthMicroserviceOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Jwt.Key) || options.Jwt.Key.Length < 32)
        {
            errors.Add("AuthMicroservice:Jwt:Key must be set and at least 32 characters long.");
        }

        if (options.Jwt.AccessTokenLifetimeMinutes <= 0)
        {
            errors.Add("AuthMicroservice:Jwt:AccessTokenLifetimeMinutes must be > 0.");
        }

        if (options.Jwt.RefreshTokenLifetimeDays <= 0)
        {
            errors.Add("AuthMicroservice:Jwt:RefreshTokenLifetimeDays must be > 0.");
        }

        if (options.Email.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.Email.Smtp.Host))
            {
                errors.Add("AuthMicroservice:Email:Smtp:Host is required when Email.Enabled=true.");
            }

            if (options.Email.Smtp.Port <= 0)
            {
                errors.Add("AuthMicroservice:Email:Smtp:Port must be > 0 when Email.Enabled=true.");
            }

            if (string.IsNullOrWhiteSpace(options.Email.FromAddress))
            {
                errors.Add("AuthMicroservice:Email:FromAddress is required when Email.Enabled=true.");
            }
        }

        if (options.ExternalProviders.Google.Enabled &&
            string.IsNullOrWhiteSpace(options.ExternalProviders.Google.ClientId))
        {
            errors.Add("AuthMicroservice:ExternalProviders:Google:ClientId is required when Google.Enabled=true.");
        }

        if (options.ExternalProviders.Microsoft.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.ExternalProviders.Microsoft.ClientId))
            {
                errors.Add("AuthMicroservice:ExternalProviders:Microsoft:ClientId is required when Microsoft.Enabled=true.");
            }

            if (string.IsNullOrWhiteSpace(options.ExternalProviders.Microsoft.TenantId))
            {
                errors.Add("AuthMicroservice:ExternalProviders:Microsoft:TenantId is required when Microsoft.Enabled=true.");
            }
        }

        if (string.IsNullOrWhiteSpace(options.TokenLinks.EmailVerificationBaseUrl))
        {
            errors.Add("AuthMicroservice:TokenLinks:EmailVerificationBaseUrl is required.");
        }

        if (string.IsNullOrWhiteSpace(options.TokenLinks.PasswordResetBaseUrl))
        {
            errors.Add("AuthMicroservice:TokenLinks:PasswordResetBaseUrl is required.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
