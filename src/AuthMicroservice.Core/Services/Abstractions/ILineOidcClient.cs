namespace AuthMicroservice.Core.Services.Abstractions;

public interface ILineOidcClient
{
    string BuildAuthorizeUrl(string state, string codeChallenge, string nonce);

    Task<LineUserInfo> ExchangeAndFetchUserAsync(
        string code,
        string codeVerifier,
        CancellationToken cancellationToken = default);
}

public sealed class LineOidcException : Exception
{
    public LineOidcException(string message) : base(message) { }

    public LineOidcException(string message, Exception inner) : base(message, inner) { }
}
