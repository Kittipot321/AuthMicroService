using System.Collections.Concurrent;
using AuthMicroservice.Core.Services.Abstractions;

namespace AuthMicroservice.IntegrationTests.Infrastructure;

public sealed class FakeLineOidcClient : ILineOidcClient
{
    private readonly ConcurrentDictionary<string, LineUserInfo> _codes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _errors = new(StringComparer.Ordinal);

    public string LastState { get; private set; } = string.Empty;
    public string LastCodeChallenge { get; private set; } = string.Empty;
    public string LastNonce { get; private set; } = string.Empty;

    public void RegisterCode(string code, LineUserInfo user) => _codes[code] = user;

    public void RegisterFailure(string code, string message) => _errors[code] = message;

    public void Clear()
    {
        _codes.Clear();
        _errors.Clear();
        LastState = string.Empty;
        LastCodeChallenge = string.Empty;
        LastNonce = string.Empty;
    }

    public string BuildAuthorizeUrl(string state, string codeChallenge, string nonce)
    {
        LastState = state;
        LastCodeChallenge = codeChallenge;
        LastNonce = nonce;
        return $"https://fake-line.local/authorize?state={Uri.EscapeDataString(state)}" +
               $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
               $"&nonce={Uri.EscapeDataString(nonce)}";
    }

    public Task<LineUserInfo> ExchangeAndFetchUserAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
    {
        if (_errors.TryGetValue(code, out var message))
        {
            throw new LineOidcException(message);
        }

        if (_codes.TryGetValue(code, out var user))
        {
            // Echo back the nonce from the last authorize call so the service's nonce check passes
            // unless the test explicitly registered a mismatched nonce on the user.
            var withNonce = user.Nonce is null ? user with { Nonce = LastNonce } : user;
            return Task.FromResult(withNonce);
        }

        throw new LineOidcException($"No fake LINE code registered for '{code}'.");
    }
}
