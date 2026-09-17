using System.Collections.Concurrent;
using AuthMicroservice.Core.Services.Abstractions;

namespace AuthMicroservice.IntegrationTests.Infrastructure;

public sealed class FakeMicrosoftTokenValidator : IMicrosoftTokenValidator
{
    private readonly ConcurrentDictionary<string, MicrosoftUserInfo> _tokens = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _errors = new(StringComparer.Ordinal);

    public void RegisterToken(string idToken, MicrosoftUserInfo user) => _tokens[idToken] = user;

    public void RegisterFailure(string idToken, string message) => _errors[idToken] = message;

    public void Clear()
    {
        _tokens.Clear();
        _errors.Clear();
    }

    public Task<MicrosoftUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (_errors.TryGetValue(idToken, out var message))
        {
            throw new MicrosoftTokenValidationException(message);
        }

        if (_tokens.TryGetValue(idToken, out var user))
        {
            return Task.FromResult(user);
        }

        throw new MicrosoftTokenValidationException($"No fake Microsoft token registered for '{idToken}'.");
    }
}
