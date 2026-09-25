using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Services;

internal sealed class GoogleOAuthClient : IGoogleOAuthClient
{
    internal const string HttpClientName = "GoogleOAuth";

    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string PopupRedirectUri = "postmessage";

    private readonly IOptionsMonitor<AuthMicroserviceOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGoogleTokenValidator _tokenValidator;

    public GoogleOAuthClient(
        IOptionsMonitor<AuthMicroserviceOptions> options,
        IHttpClientFactory httpClientFactory,
        IGoogleTokenValidator tokenValidator)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _tokenValidator = tokenValidator;
    }

    public async Task<GoogleUserInfo> ExchangeAndFetchUserAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var google = GetEnabledOptions();
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        var tokenResponse = await ExchangeCodeAsync(httpClient, google, code, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(tokenResponse.IdToken))
        {
            throw new GoogleOAuthException("Google token endpoint response missing id_token.");
        }

        try
        {
            return await _tokenValidator.ValidateAsync(tokenResponse.IdToken!, cancellationToken).ConfigureAwait(false);
        }
        catch (GoogleTokenValidationException ex)
        {
            throw new GoogleOAuthException("Google id_token validation failed after code exchange.", ex);
        }
    }

    private GoogleProviderOptions GetEnabledOptions()
    {
        var google = _options.CurrentValue.ExternalProviders.Google;
        if (!google.Enabled)
        {
            throw new GoogleOAuthException("Google external login is not enabled.");
        }
        if (string.IsNullOrWhiteSpace(google.ClientId) || string.IsNullOrWhiteSpace(google.ClientSecret))
        {
            throw new GoogleOAuthException("Google authorization-code configuration is incomplete (ClientId and ClientSecret required).");
        }
        return google;
    }

    private static async Task<GoogleTokenResponse> ExchangeCodeAsync(
        HttpClient httpClient,
        GoogleProviderOptions google,
        string code,
        CancellationToken cancellationToken)
    {
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", PopupRedirectUri),
            new KeyValuePair<string, string>("client_id", google.ClientId),
            new KeyValuePair<string, string>("client_secret", google.ClientSecret!),
        });

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(TokenEndpoint, form, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new GoogleOAuthException("Failed to call Google token endpoint.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new GoogleOAuthException($"Google token endpoint returned {(int)response.StatusCode}: {Truncate(body, 500)}");
        }

        GoogleTokenResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new GoogleOAuthException("Failed to parse Google token endpoint response.", ex);
        }

        return payload ?? throw new GoogleOAuthException("Google token endpoint returned empty body.");
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];

    private sealed record GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("scope")]
        public string? Scope { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
