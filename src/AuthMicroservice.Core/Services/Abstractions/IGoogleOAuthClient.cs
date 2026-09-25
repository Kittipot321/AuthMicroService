namespace AuthMicroservice.Core.Services.Abstractions;

public interface IGoogleOAuthClient
{
    Task<GoogleUserInfo> ExchangeAndFetchUserAsync(
        string code,
        CancellationToken cancellationToken = default);
}

public sealed class GoogleOAuthException : Exception
{
    public GoogleOAuthException(string message) : base(message) { }

    public GoogleOAuthException(string message, Exception inner) : base(message, inner) { }
}
