using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Services.Abstractions;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Services;

internal sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly IOptionsMonitor<AuthMicroserviceOptions> _options;

    public GoogleTokenValidator(IOptionsMonitor<AuthMicroserviceOptions> options)
    {
        _options = options;
    }

    public async Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var google = _options.CurrentValue.ExternalProviders.Google;
        if (!google.Enabled || string.IsNullOrWhiteSpace(google.ClientId))
        {
            throw new GoogleTokenValidationException("Google external login is not enabled.");
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { google.ClientId }
            }).ConfigureAwait(false);
        }
        catch (InvalidJwtException ex)
        {
            throw new GoogleTokenValidationException("Google id_token failed signature, audience, or expiry validation.", ex);
        }

        if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
        {
            throw new GoogleTokenValidationException("Google id_token is missing required subject or email claims.");
        }

        return new GoogleUserInfo(
            Subject: payload.Subject,
            Email: payload.Email,
            EmailVerified: payload.EmailVerified,
            Name: payload.Name,
            PictureUrl: payload.Picture);
    }
}
