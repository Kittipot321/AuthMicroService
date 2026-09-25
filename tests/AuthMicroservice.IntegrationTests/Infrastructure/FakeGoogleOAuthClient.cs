using System.Collections.Concurrent;
using AuthMicroservice.Core.Services.Abstractions;

namespace AuthMicroservice.IntegrationTests.Infrastructure;

public sealed class FakeGoogleOAuthClient : IGoogleOAuthClient
{
    private readonly ConcurrentDictionary<string, GoogleUserInfo> _codes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _errors = new(StringComparer.Ordinal);

    public string LastCode { get; private set; } = string.Empty;

    public void RegisterCode(string code, GoogleUserInfo user) => _codes[code] = user;

    public void RegisterFailure(string code, string message) => _errors[code] = message;

    public void Clear()
    {
        _codes.Clear();
        _errors.Clear();
        LastCode = string.Empty;
    }

    public Task<GoogleUserInfo> ExchangeAndFetchUserAsync(string code, CancellationToken cancellationToken = default)
    {
        LastCode = code;

        if (_errors.TryGetValue(code, out var message))
        {
            throw new GoogleOAuthException(message);
        }

        if (_codes.TryGetValue(code, out var user))
        {
            return Task.FromResult(user);
        }

        throw new GoogleOAuthException($"No fake Google code registered for '{code}'.");
    }
}
