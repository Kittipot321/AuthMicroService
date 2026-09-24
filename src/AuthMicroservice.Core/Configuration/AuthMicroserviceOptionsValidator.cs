using AuthMicroservice.Core.Domain;
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

        if (options.ExternalProviders.Facebook.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.ExternalProviders.Facebook.AppId))
            {
                errors.Add("AuthMicroservice:ExternalProviders:Facebook:AppId is required when Facebook.Enabled=true.");
            }

            if (string.IsNullOrWhiteSpace(options.ExternalProviders.Facebook.AppSecret))
            {
                errors.Add("AuthMicroservice:ExternalProviders:Facebook:AppSecret is required when Facebook.Enabled=true.");
            }
        }

        if (options.ExternalProviders.Line.Enabled &&
            string.IsNullOrWhiteSpace(options.ExternalProviders.Line.ChannelId))
        {
            errors.Add("AuthMicroservice:ExternalProviders:Line:ChannelId is required when Line.Enabled=true.");
        }

        if (options.ExternalProviders.ThaId.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.ExternalProviders.ThaId.ClientId))
            {
                errors.Add("AuthMicroservice:ExternalProviders:ThaId:ClientId is required when ThaId.Enabled=true.");
            }

            if (string.IsNullOrWhiteSpace(options.ExternalProviders.ThaId.ClientSecret))
            {
                errors.Add("AuthMicroservice:ExternalProviders:ThaId:ClientSecret is required when ThaId.Enabled=true.");
            }

            if (string.IsNullOrWhiteSpace(options.ExternalProviders.ThaId.Authority))
            {
                errors.Add("AuthMicroservice:ExternalProviders:ThaId:Authority is required when ThaId.Enabled=true.");
            }

            if (string.IsNullOrWhiteSpace(options.ExternalProviders.ThaId.RedirectUri))
            {
                errors.Add("AuthMicroservice:ExternalProviders:ThaId:RedirectUri is required when ThaId.Enabled=true.");
            }

            if (options.ExternalProviders.ThaId.AllowedReturnUrlPrefixes.Count == 0)
            {
                errors.Add("AuthMicroservice:ExternalProviders:ThaId:AllowedReturnUrlPrefixes must contain at least one entry when ThaId.Enabled=true.");
            }
        }

        var seenRoleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < options.Identity.Roles.AdditionalRoles.Count; i++)
        {
            var role = options.Identity.Roles.AdditionalRoles[i];
            var path = $"AuthMicroservice:Identity:Roles:AdditionalRoles[{i}]";

            if (string.IsNullOrWhiteSpace(role.Name))
            {
                errors.Add($"{path}:Name is required.");
                continue;
            }

            if (role.Name.Length > 256)
            {
                errors.Add($"{path}:Name must be 256 characters or fewer.");
            }

            if (AuthRoles.System.Contains(role.Name, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"{path}:Name '{role.Name}' conflicts with a reserved system role.");
            }

            if (!seenRoleNames.Add(role.Name))
            {
                errors.Add($"{path}:Name '{role.Name}' is duplicated in AdditionalRoles.");
            }

            if (role.Description is { Length: > 256 })
            {
                errors.Add($"{path}:Description must be 256 characters or fewer.");
            }
        }

        var knownRoles = new HashSet<string>(AuthRoles.System, StringComparer.OrdinalIgnoreCase);
        foreach (var role in options.Identity.Roles.AdditionalRoles)
        {
            if (!string.IsNullOrWhiteSpace(role.Name))
            {
                knownRoles.Add(role.Name);
            }
        }

        var defaultRole = options.Identity.Roles.DefaultRegistrationRole;
        if (string.IsNullOrWhiteSpace(defaultRole))
        {
            errors.Add("AuthMicroservice:Identity:Roles:DefaultRegistrationRole is required.");
        }
        else if (defaultRole.Length > 256)
        {
            errors.Add("AuthMicroservice:Identity:Roles:DefaultRegistrationRole must be 256 characters or fewer.");
        }
        else if (!knownRoles.Contains(defaultRole))
        {
            errors.Add($"AuthMicroservice:Identity:Roles:DefaultRegistrationRole '{defaultRole}' does not match any system role or AdditionalRoles entry.");
        }

        var seenAllowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < options.Identity.Roles.AllowedSelfRegisterRoles.Count; i++)
        {
            var allowed = options.Identity.Roles.AllowedSelfRegisterRoles[i];
            var path = $"AuthMicroservice:Identity:Roles:AllowedSelfRegisterRoles[{i}]";

            if (string.IsNullOrWhiteSpace(allowed))
            {
                errors.Add($"{path} is required.");
                continue;
            }

            if (allowed.Length > 256)
            {
                errors.Add($"{path} must be 256 characters or fewer.");
            }

            if (!knownRoles.Contains(allowed))
            {
                errors.Add($"{path} '{allowed}' does not match any system role or AdditionalRoles entry.");
            }

            if (!seenAllowed.Add(allowed))
            {
                errors.Add($"{path} '{allowed}' is duplicated in AllowedSelfRegisterRoles.");
            }
        }

        if (options.Otp.CodeLength < 4 || options.Otp.CodeLength > 10)
        {
            errors.Add("AuthMicroservice:Otp:CodeLength must be between 4 and 10.");
        }

        if (options.Otp.ExpirationMinutes <= 0)
        {
            errors.Add("AuthMicroservice:Otp:ExpirationMinutes must be > 0.");
        }

        if (options.Otp.MaxAttempts < 1)
        {
            errors.Add("AuthMicroservice:Otp:MaxAttempts must be >= 1.");
        }

        if (options.Otp.ResendCooldownSeconds < 0)
        {
            errors.Add("AuthMicroservice:Otp:ResendCooldownSeconds must be >= 0.");
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
