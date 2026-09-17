using System.Collections.Concurrent;
using AuthMicroservice.Core.Services.Abstractions;

namespace AuthMicroservice.IntegrationTests.Infrastructure;

public sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    private readonly ConcurrentDictionary<string, GoogleUserInfo> _tokens = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _errors = new(StringComparer.Ordinal);

    public void RegisterToken(string idToken, GoogleUserInfo user) => _tokens[idToken] = user;

    public void RegisterFailure(string idToken, string message) => _errors[idToken] = message;

    public void Clear()
    {
        _tokens.Clear();
        _errors.Clear();
    }

    public Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (_errors.TryGetValue(idToken, out var message))
        {
            throw new GoogleTokenValidationException(message);
        }

        if (_tokens.TryGetValue(idToken, out var user))
        {
            return Task.FromResult(user);
        }

        throw new GoogleTokenValidationException($"No fake Google token registered for '{idToken}'.");
    }
}
