using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Services;

internal sealed class FacebookTokenValidator : IFacebookTokenValidator
{
    internal const string HttpClientName = "Facebook";
    private const string GraphBaseUrl = "https://graph.facebook.com";

    private readonly IOptionsMonitor<AuthMicroserviceOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public FacebookTokenValidator(
        IOptionsMonitor<AuthMicroserviceOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<FacebookUserInfo> ValidateAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var facebook = _options.CurrentValue.ExternalProviders.Facebook;
        if (!facebook.Enabled
            || string.IsNullOrWhiteSpace(facebook.AppId)
            || string.IsNullOrWhiteSpace(facebook.AppSecret))
        {
            throw new FacebookTokenValidationException("Facebook external login is not enabled.");
        }

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var apiVersion = string.IsNullOrWhiteSpace(facebook.GraphApiVersion) ? "v18.0" : facebook.GraphApiVersion;

        // 1) debug_token — verify token was issued to our app and is still valid
        var appAccessToken = $"{facebook.AppId}|{facebook.AppSecret}";
        var debugUrl = $"{GraphBaseUrl}/debug_token"
            + $"?input_token={Uri.EscapeDataString(accessToken)}"
            + $"&access_token={Uri.EscapeDataString(appAccessToken)}";

        DebugTokenResponse? debug;
        try
        {
            debug = await httpClient
                .GetFromJsonAsync<DebugTokenResponse>(debugUrl, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            throw new FacebookTokenValidationException(
                "Failed to call Facebook debug_token endpoint.", ex);
        }

        if (debug?.Data is null || !debug.Data.IsValid)
        {
            throw new FacebookTokenValidationException(
                "Facebook access token failed debug_token validation.");
        }

        if (!string.Equals(debug.Data.AppId, facebook.AppId, StringComparison.Ordinal))
        {
            throw new FacebookTokenValidationException(
                "Facebook access token was issued to a different app.");
        }

        if (debug.Data.ExpiresAt is > 0)
        {
            var expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(debug.Data.ExpiresAt.Value);
            if (expiresAtUtc <= DateTimeOffset.UtcNow)
            {
                throw new FacebookTokenValidationException("Facebook access token has expired.");
            }
        }

        // 2) /me — fetch user profile
        var meUrl = $"{GraphBaseUrl}/{apiVersion}/me"
            + "?fields=id,email,name,picture.type(large)"
            + $"&access_token={Uri.EscapeDataString(accessToken)}";

        MeResponse? me;
        try
        {
            me = await httpClient
                .GetFromJsonAsync<MeResponse>(meUrl, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            throw new FacebookTokenValidationException(
                "Failed to fetch Facebook user profile from /me.", ex);
        }

        if (me is null || string.IsNullOrWhiteSpace(me.Id))
        {
            throw new FacebookTokenValidationException("Facebook /me response missing id.");
        }

        return new FacebookUserInfo(
            Subject: me.Id,
            Email: me.Email,
            Name: me.Name,
            PictureUrl: me.Picture?.Data?.Url);
    }

    private sealed record DebugTokenResponse
    {
        [JsonPropertyName("data")]
        public DebugTokenData? Data { get; init; }
    }

    private sealed record DebugTokenData
    {
        [JsonPropertyName("app_id")]
        public string? AppId { get; init; }

        [JsonPropertyName("is_valid")]
        public bool IsValid { get; init; }

        [JsonPropertyName("expires_at")]
        public long? ExpiresAt { get; init; }

        [JsonPropertyName("user_id")]
        public string? UserId { get; init; }
    }

    private sealed record MeResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("picture")]
        public PictureNode? Picture { get; init; }
    }

    private sealed record PictureNode
    {
        [JsonPropertyName("data")]
        public PictureData? Data { get; init; }
    }

    private sealed record PictureData
    {
        [JsonPropertyName("url")]
        public string? Url { get; init; }
    }
}
