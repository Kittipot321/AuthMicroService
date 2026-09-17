namespace AuthMicroservice.Core.Services.Abstractions;

public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed record GoogleUserInfo(
    string Subject,
    string Email,
    bool EmailVerified,
    string? Name,
    string? PictureUrl);

public sealed class GoogleTokenValidationException : Exception
{
    public GoogleTokenValidationException(string message) : base(message) { }

    public GoogleTokenValidationException(string message, Exception inner) : base(message, inner) { }
}
