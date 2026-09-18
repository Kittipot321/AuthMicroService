using System.Collections.Concurrent;
using AuthMicroservice.Core.Services.Abstractions;

namespace AuthMicroservice.IntegrationTests.Infrastructure;

public sealed class FakeLineTokenValidator : ILineTokenValidator
{
    private readonly ConcurrentDictionary<string, LineUserInfo> _tokens = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _errors = new(StringComparer.Ordinal);

    public void RegisterToken(string idToken, LineUserInfo user) => _tokens[idToken] = user;

    public void RegisterFailure(string idToken, string message) => _errors[idToken] = message;

    public void Clear()
    {
        _tokens.Clear();
        _errors.Clear();
    }

    public Task<LineUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (_errors.TryGetValue(idToken, out var message))
        {
            throw new LineTokenValidationException(message);
        }

        if (_tokens.TryGetValue(idToken, out var user))
        {
            return Task.FromResult(user);
        }

        throw new LineTokenValidationException($"No fake LINE token registered for '{idToken}'.");
    }
}
