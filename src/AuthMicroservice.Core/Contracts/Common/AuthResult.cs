using AuthMicroservice.Core.Configuration;

namespace AuthMicroservice.Core.Contracts.Common;

public class AuthResult
{
    public bool Succeeded { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    public TwoFactorChallenge? TwoFactorChallenge { get; init; }

    public EmailVerificationChallenge? EmailVerificationChallenge { get; init; }

    public static AuthResult Success() => new() { Succeeded = true };

    public static AuthResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };

    public static AuthResult Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new()
        {
            Succeeded = false,
            ErrorCode = AuthErrorCodes.ValidationFailed,
            ErrorMessage = "Validation failed.",
            ValidationErrors = errors
        };
}

public sealed class AuthResult<T> : AuthResult
{
    public T? Value { get; init; }

    public static AuthResult<T> Success(T value) => new()
    {
        Succeeded = true,
        Value = value
    };

    public new static AuthResult<T> Failure(string errorCode, string errorMessage) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    public static AuthResult<T> Failure(string errorCode, string errorMessage, TwoFactorChallenge challenge) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        TwoFactorChallenge = challenge
    };

    public static AuthResult<T> Failure(string errorCode, string errorMessage, EmailVerificationChallenge? challenge) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        EmailVerificationChallenge = challenge
    };

    public new static AuthResult<T> Validation(IReadOnlyDictionary<string, string[]> errors) => new()
    {
        Succeeded = false,
        ErrorCode = AuthErrorCodes.ValidationFailed,
        ErrorMessage = "Validation failed.",
        ValidationErrors = errors
    };
}

public sealed record TwoFactorChallenge(IReadOnlyList<string> Methods, bool EmailChallengeSent, DateTime? EmailCodeExpiresAt);

public sealed record EmailVerificationChallenge(EmailVerificationMode Mode, bool VerificationSent, DateTime? ExpiresAt);
