namespace AuthMicroservice.Core.Services.Abstractions;

public interface IFacebookTokenValidator
{
    Task<FacebookUserInfo> ValidateAsync(string accessToken, CancellationToken cancellationToken = default);
}

public sealed record FacebookUserInfo(
    string Subject,
    string? Email,
    string? Name,
    string? PictureUrl);

public sealed class FacebookTokenValidationException : Exception
{
    public FacebookTokenValidationException(string message) : base(message) { }

    public FacebookTokenValidationException(string message, Exception inner) : base(message, inner) { }
}
