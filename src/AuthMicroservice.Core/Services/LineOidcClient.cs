using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Services;

internal sealed class LineOidcClient : ILineOidcClient
{
    internal const string HttpClientName = "LineOidc";

    private readonly IOptionsMonitor<AuthMicroserviceOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILineTokenValidator _tokenValidator;

    public LineOidcClient(
        IOptionsMonitor<AuthMicroserviceOptions> options,
        IHttpClientFactory httpClientFactory,
        ILineTokenValidator tokenValidator)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _tokenValidator = tokenValidator;
    }

    public string BuildAuthorizeUrl(string state, string codeChallenge, string nonce)
    {
        var line = GetEnabledOptions();
        var authority = line.Authority.TrimEnd('/');

        var query = new[]
        {
            "response_type=code",
            $"client_id={Uri.EscapeDataString(line.ChannelId)}",
            $"redirect_uri={Uri.EscapeDataString(line.RedirectUri)}",
            $"scope={Uri.EscapeDataString(line.Scopes)}",
            $"state={Uri.EscapeDataString(state)}",
            $"nonce={Uri.EscapeDataString(nonce)}",
            $"code_challenge={Uri.EscapeDataString(codeChallenge)}",
            "code_challenge_method=S256",
        };

        return $"{authority}/oauth2/v2.1/authorize?{string.Join("&", query)}";
    }

    public async Task<LineUserInfo> ExchangeAndFetchUserAsync(
        string code,
        string codeVerifier,
        CancellationToken cancellationToken = default)
    {
        var line = GetEnabledOptions();
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        var tokenResponse = await ExchangeCodeAsync(httpClient, line, code, codeVerifier, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(tokenResponse.IdToken))
        {
            throw new LineOidcException("LINE token endpoint response missing id_token.");
        }

        try
        {
            return await _tokenValidator.ValidateAsync(tokenResponse.IdToken!, cancellationToken).ConfigureAwait(false);
        }
        catch (LineTokenValidationException ex)
        {
            throw new LineOidcException("LINE id_token validation failed after code exchange.", ex);
        }
    }

    private LineProviderOptions GetEnabledOptions()
    {
        var line = _options.CurrentValue.ExternalProviders.Line;
        if (!line.Enabled)
        {
            throw new LineOidcException("LINE external login is not enabled.");
        }
        if (string.IsNullOrWhiteSpace(line.ChannelId) ||
            string.IsNullOrWhiteSpace(line.ChannelSecret) ||
            string.IsNullOrWhiteSpace(line.Authority) ||
            string.IsNullOrWhiteSpace(line.TokenEndpoint) ||
            string.IsNullOrWhiteSpace(line.RedirectUri))
        {
            throw new LineOidcException("LINE authorization-code configuration is incomplete (ChannelId, ChannelSecret, Authority, TokenEndpoint, RedirectUri required).");
        }
        return line;
    }

    private static async Task<LineTokenResponse> ExchangeCodeAsync(
        HttpClient httpClient,
        LineProviderOptions line,
        string code,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", line.RedirectUri),
            new KeyValuePair<string, string>("client_id", line.ChannelId),
            new KeyValuePair<string, string>("client_secret", line.ChannelSecret),
            new KeyValuePair<string, string>("code_verifier", codeVerifier),
        });

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(line.TokenEndpoint, form, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new LineOidcException("Failed to call LINE token endpoint.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new LineOidcException($"LINE token endpoint returned {(int)response.StatusCode}: {Truncate(body, 500)}");
        }

        LineTokenResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<LineTokenResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new LineOidcException("Failed to parse LINE token endpoint response.", ex);
        }

        return payload ?? throw new LineOidcException("LINE token endpoint returned empty body.");
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];

    private sealed record LineTokenResponse
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
