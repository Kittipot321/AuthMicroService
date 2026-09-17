using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AuthMicroservice.Core.Services;

internal sealed class MicrosoftTokenValidator : IMicrosoftTokenValidator
{
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> ConfigManagers =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> MultiTenantAliases =
        new(StringComparer.OrdinalIgnoreCase) { "common", "organizations", "consumers" };

    private readonly IOptionsMonitor<AuthMicroserviceOptions> _options;

    public MicrosoftTokenValidator(IOptionsMonitor<AuthMicroserviceOptions> options)
    {
        _options = options;
    }

    public async Task<MicrosoftUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var microsoft = _options.CurrentValue.ExternalProviders.Microsoft;
        if (!microsoft.Enabled || string.IsNullOrWhiteSpace(microsoft.ClientId))
        {
            throw new MicrosoftTokenValidationException("Microsoft external login is not enabled.");
        }

        var tenant = string.IsNullOrWhiteSpace(microsoft.TenantId) ? "common" : microsoft.TenantId;
        var metadataAddress = $"https://login.microsoftonline.com/{tenant}/v2.0/.well-known/openid-configuration";
        var configManager = ConfigManagers.GetOrAdd(metadataAddress, addr =>
            new ConfigurationManager<OpenIdConnectConfiguration>(
                addr,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = true }));

        OpenIdConnectConfiguration config;
        try
        {
            config = await configManager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new MicrosoftTokenValidationException("Failed to fetch Microsoft OpenID Connect metadata.", ex);
        }

        var isMultiTenant = MultiTenantAliases.Contains(tenant);
        var validationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = microsoft.ClientId,
            ValidateIssuer = true,
            IssuerValidator = isMultiTenant ? MultiTenantIssuerValidator : null,
            ValidIssuer = isMultiTenant ? null : $"https://login.microsoftonline.com/{tenant}/v2.0",
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidateLifetime = true
        };

        var handler = new JwtSecurityTokenHandler();
        System.Security.Claims.ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(idToken, validationParameters, out _);
        }
        catch (SecurityTokenException ex)
        {
            throw new MicrosoftTokenValidationException("Microsoft id_token failed signature, audience, issuer, or expiry validation.", ex);
        }

        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst("sub")?.Value;
        var email = principal.FindFirst("email")?.Value
            ?? principal.FindFirst("preferred_username")?.Value;
        var name = principal.FindFirst("name")?.Value;
        var tid = principal.FindFirst("tid")?.Value;

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new MicrosoftTokenValidationException("Microsoft id_token is missing subject claim.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new MicrosoftTokenValidationException("Microsoft id_token is missing email/preferred_username claim.");
        }

        // Microsoft id_tokens do not consistently include email_verified (personal MSA accounts
        // often omit it). Since Microsoft owns the identity and the signature/audience checks
        // above already succeeded, treat the returned email as trusted.
        return new MicrosoftUserInfo(
            Subject: subject,
            Email: email,
            Name: name,
            TenantId: tid);
    }

    private static string MultiTenantIssuerValidator(string issuer, SecurityToken token, TokenValidationParameters parameters)
    {
        if (token is not JwtSecurityToken jwt)
        {
            throw new SecurityTokenInvalidIssuerException("Unsupported token type.");
        }

        var tid = jwt.Payload.TryGetValue("tid", out var tidObj) ? tidObj?.ToString() : null;
        if (string.IsNullOrEmpty(tid))
        {
            throw new SecurityTokenInvalidIssuerException("Microsoft id_token is missing tid claim.");
        }

        var expected = $"https://login.microsoftonline.com/{tid}/v2.0";
        if (!string.Equals(issuer, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenInvalidIssuerException($"Issuer '{issuer}' does not match expected '{expected}'.");
        }

        return issuer;
    }
}
