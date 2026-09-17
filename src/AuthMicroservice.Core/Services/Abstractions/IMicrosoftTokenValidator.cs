namespace AuthMicroservice.Core.Services.Abstractions;

public interface IMicrosoftTokenValidator
{
    Task<MicrosoftUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed record MicrosoftUserInfo(
    string Subject,
    string Email,
    string? Name,
    string? TenantId);

public sealed class MicrosoftTokenValidationException : Exception
{
    public MicrosoftTokenValidationException(string message) : base(message) { }

    public MicrosoftTokenValidationException(string message, Exception inner) : base(message, inner) { }
}
