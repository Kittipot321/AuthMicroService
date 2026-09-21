namespace AuthMicroservice.Core.Services.Abstractions;

public interface IThaIdOidcClient
{
    string BuildAuthorizeUrl(string state, string codeChallenge);

    Task<ThaIdUserInfo> ExchangeAndFetchUserAsync(string code, string codeVerifier, CancellationToken cancellationToken = default);
}

public sealed record ThaIdUserInfo(
    string Pid,
    string? Email,
    string? GivenName,
    string? FamilyName,
    string? Birthdate,
    string? Address);

public sealed class ThaIdOidcException : Exception
{
    public ThaIdOidcException(string message) : base(message) { }

    public ThaIdOidcException(string message, Exception inner) : base(message, inner) { }
}
