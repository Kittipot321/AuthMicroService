namespace AuthMicroservice.Core.Services.Abstractions;

public interface ILineTokenValidator
{
    Task<LineUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed record LineUserInfo(
    string Subject,
    string? Email,
    string? Name,
    string? PictureUrl);

public sealed class LineTokenValidationException : Exception
{
    public LineTokenValidationException(string message) : base(message) { }

    public LineTokenValidationException(string message, Exception inner) : base(message, inner) { }
}
