using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Services;

internal sealed class LineTokenValidator : ILineTokenValidator
{
    internal const string HttpClientName = "Line";
    private const string DefaultVerifyEndpoint = "https://api.line.me/oauth2/v2.1/verify";
    private const string ExpectedIssuer = "https://access.line.me";

    private readonly IOptionsMonitor<AuthMicroserviceOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public LineTokenValidator(
        IOptionsMonitor<AuthMicroserviceOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<LineUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var line = _options.CurrentValue.ExternalProviders.Line;
        if (!line.Enabled || string.IsNullOrWhiteSpace(line.ChannelId))
        {
            throw new LineTokenValidationException("LINE external login is not enabled.");
        }

        var endpoint = string.IsNullOrWhiteSpace(line.VerifyEndpoint) ? DefaultVerifyEndpoint : line.VerifyEndpoint;
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        var body = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("id_token", idToken),
            new KeyValuePair<string, string>("client_id", line.ChannelId),
        });

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(endpoint, body, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new LineTokenValidationException("Failed to call LINE verify endpoint.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            LineErrorResponse? err = null;
            try
            {
                err = await response.Content
                    .ReadFromJsonAsync<LineErrorResponse>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // error body may not be JSON — swallow and fall back to status code
            }

            var detail = err?.ErrorDescription ?? err?.Error ?? "unknown error";
            throw new LineTokenValidationException(
                $"LINE verify endpoint returned {(int)response.StatusCode}: {detail}.");
        }

        LineVerifyResponse? claims;
        try
        {
            claims = await response.Content
                .ReadFromJsonAsync<LineVerifyResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new LineTokenValidationException("Failed to parse LINE verify response.", ex);
        }

        if (claims is null || string.IsNullOrWhiteSpace(claims.Sub))
        {
            throw new LineTokenValidationException("LINE verify response missing sub.");
        }

        if (!string.Equals(claims.Aud, line.ChannelId, StringComparison.Ordinal))
        {
            throw new LineTokenValidationException("LINE id_token aud does not match configured ChannelId.");
        }

        if (!string.Equals(claims.Iss, ExpectedIssuer, StringComparison.Ordinal))
        {
            throw new LineTokenValidationException($"LINE id_token iss '{claims.Iss}' is unexpected.");
        }

        if (claims.Exp > 0)
        {
            var expUtc = DateTimeOffset.FromUnixTimeSeconds(claims.Exp);
            if (expUtc <= DateTimeOffset.UtcNow)
            {
                throw new LineTokenValidationException("LINE id_token has expired.");
            }
        }

        return new LineUserInfo(
            Subject: claims.Sub,
            Email: claims.Email,
            Name: claims.Name,
            PictureUrl: claims.Picture,
            Nonce: claims.Nonce);
    }

    private sealed record LineVerifyResponse
    {
        [JsonPropertyName("iss")]
        public string? Iss { get; init; }

        [JsonPropertyName("sub")]
        public string? Sub { get; init; }

        [JsonPropertyName("aud")]
        public string? Aud { get; init; }

        [JsonPropertyName("exp")]
        public long Exp { get; init; }

        [JsonPropertyName("iat")]
        public long Iat { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("picture")]
        public string? Picture { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("nonce")]
        public string? Nonce { get; init; }
    }

    private sealed record LineErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; init; }
    }
}
