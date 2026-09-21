using System.Collections.Concurrent;
using AuthMicroservice.Core.Services.Abstractions;

namespace AuthMicroservice.IntegrationTests.Infrastructure;

public sealed class FakeThaIdOidcClient : IThaIdOidcClient
{
    private readonly ConcurrentDictionary<string, ThaIdUserInfo> _codes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _errors = new(StringComparer.Ordinal);

    public string LastState { get; private set; } = string.Empty;
    public string LastCodeChallenge { get; private set; } = string.Empty;

    public void RegisterCode(string code, ThaIdUserInfo user) => _codes[code] = user;

    public void RegisterFailure(string code, string message) => _errors[code] = message;

    public void Clear()
    {
        _codes.Clear();
        _errors.Clear();
        LastState = string.Empty;
        LastCodeChallenge = string.Empty;
    }

    public string BuildAuthorizeUrl(string state, string codeChallenge)
    {
        LastState = state;
        LastCodeChallenge = codeChallenge;
        return $"https://fake-thaid.local/authorize?state={Uri.EscapeDataString(state)}&code_challenge={Uri.EscapeDataString(codeChallenge)}";
    }

    public Task<ThaIdUserInfo> ExchangeAndFetchUserAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
    {
        if (_errors.TryGetValue(code, out var message))
        {
            throw new ThaIdOidcException(message);
        }

        if (_codes.TryGetValue(code, out var user))
        {
            return Task.FromResult(user);
        }

        throw new ThaIdOidcException($"No fake ThaID code registered for '{code}'.");
    }
}
