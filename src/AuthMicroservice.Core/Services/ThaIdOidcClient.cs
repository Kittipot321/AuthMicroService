using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AuthMicroservice.Core.Services;

internal sealed class ThaIdOidcClient : IThaIdOidcClient
{
    internal const string HttpClientName = "ThaId";

    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> ConfigManagers =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IOptionsMonitor<AuthMicroserviceOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public ThaIdOidcClient(
        IOptionsMonitor<AuthMicroserviceOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    public string BuildAuthorizeUrl(string state, string codeChallenge)
    {
        var thaId = GetEnabledOptions();
        var authority = thaId.Authority.TrimEnd('/');

        var query = new[]
        {
            $"response_type=code",
            $"client_id={Uri.EscapeDataString(thaId.ClientId)}",
            $"redirect_uri={Uri.EscapeDataString(thaId.RedirectUri)}",
            $"scope={Uri.EscapeDataString(thaId.Scopes)}",
            $"state={Uri.EscapeDataString(state)}",
            $"code_challenge={Uri.EscapeDataString(codeChallenge)}",
            $"code_challenge_method=S256"
        };

        return $"{authority}/authorize?{string.Join("&", query)}";
    }

    public async Task<ThaIdUserInfo> ExchangeAndFetchUserAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
    {
        var thaId = GetEnabledOptions();
        var authority = thaId.Authority.TrimEnd('/');
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        var tokenResponse = await ExchangeCodeAsync(httpClient, authority, thaId, code, codeVerifier, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new ThaIdOidcException("ThaID token endpoint response missing access_token.");
        }

        string pid;
        if (!string.IsNullOrWhiteSpace(tokenResponse.IdToken))
        {
            pid = await ValidateIdTokenAndGetPidAsync(authority, thaId, tokenResponse.IdToken!, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new ThaIdOidcException("ThaID token endpoint response missing id_token.");
        }

        var userInfo = await FetchUserInfoAsync(httpClient, authority, tokenResponse.AccessToken!, cancellationToken).ConfigureAwait(false);

        // pid from id_token is the trusted source; user-info supplements profile fields.
        return new ThaIdUserInfo(
            Pid: pid,
            Email: userInfo.Email,
            GivenName: userInfo.GivenName,
            FamilyName: userInfo.FamilyName,
            Birthdate: userInfo.Birthdate,
            Address: userInfo.Address);
    }

    private ThaIdProviderOptions GetEnabledOptions()
    {
        var thaId = _options.CurrentValue.ExternalProviders.ThaId;
        if (!thaId.Enabled)
        {
            throw new ThaIdOidcException("ThaID external login is not enabled.");
        }
        if (string.IsNullOrWhiteSpace(thaId.ClientId) ||
            string.IsNullOrWhiteSpace(thaId.ClientSecret) ||
            string.IsNullOrWhiteSpace(thaId.Authority) ||
            string.IsNullOrWhiteSpace(thaId.RedirectUri))
        {
            throw new ThaIdOidcException("ThaID configuration is incomplete.");
        }
        return thaId;
    }

    private static async Task<ThaIdTokenResponse> ExchangeCodeAsync(
        HttpClient httpClient,
        string authority,
        ThaIdProviderOptions thaId,
        string code,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", thaId.RedirectUri),
            new KeyValuePair<string, string>("client_id", thaId.ClientId),
            new KeyValuePair<string, string>("client_secret", thaId.ClientSecret),
            new KeyValuePair<string, string>("code_verifier", codeVerifier),
        });

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync($"{authority}/token", form, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new ThaIdOidcException("Failed to call ThaID token endpoint.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new ThaIdOidcException($"ThaID token endpoint returned {(int)response.StatusCode}: {Truncate(body, 500)}");
        }

        ThaIdTokenResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<ThaIdTokenResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new ThaIdOidcException("Failed to parse ThaID token endpoint response.", ex);
        }

        return payload ?? throw new ThaIdOidcException("ThaID token endpoint returned empty body.");
    }

    private static async Task<string> ValidateIdTokenAndGetPidAsync(
        string authority,
        ThaIdProviderOptions thaId,
        string idToken,
        CancellationToken cancellationToken)
    {
        var metadataAddress = $"{authority}/.well-known/openid-configuration";
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
            throw new ThaIdOidcException("Failed to fetch ThaID OpenID Connect metadata.", ex);
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = thaId.ClientId,
            ValidateIssuer = true,
            ValidIssuer = string.IsNullOrWhiteSpace(config.Issuer) ? null : config.Issuer,
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
            throw new ThaIdOidcException("ThaID id_token failed signature, audience, issuer, or expiry validation.", ex);
        }

        // ThaID puts the Thai National ID in the "pid" claim; some deployments also mirror it into "sub".
        var pid = principal.FindFirst("pid")?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(pid))
        {
            throw new ThaIdOidcException("ThaID id_token is missing pid claim.");
        }

        return pid;
    }

    private static async Task<ThaIdUserInfoResponse> FetchUserInfoAsync(
        HttpClient httpClient,
        string authority,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{authority}/user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new ThaIdOidcException("Failed to call ThaID user-info endpoint.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new ThaIdOidcException($"ThaID user-info endpoint returned {(int)response.StatusCode}: {Truncate(body, 500)}");
        }

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ThaIdUserInfoResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return payload ?? new ThaIdUserInfoResponse();
        }
        catch (JsonException ex)
        {
            throw new ThaIdOidcException("Failed to parse ThaID user-info response.", ex);
        }
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];

    private sealed record ThaIdTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }

    private sealed record ThaIdUserInfoResponse
    {
        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("given_name")]
        public string? GivenName { get; init; }

        [JsonPropertyName("family_name")]
        public string? FamilyName { get; init; }

        [JsonPropertyName("birthdate")]
        public string? Birthdate { get; init; }

        [JsonPropertyName("address")]
        public string? Address { get; init; }
    }
}
