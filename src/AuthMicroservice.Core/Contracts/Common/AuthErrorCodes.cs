namespace AuthMicroservice.Core.Contracts.Common;

public static class AuthErrorCodes
{
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string UserLockedOut = "USER_LOCKED_OUT";
    public const string EmailNotConfirmed = "EMAIL_NOT_CONFIRMED";
    public const string InvalidRefreshToken = "INVALID_REFRESH_TOKEN";
    public const string WeakPassword = "WEAK_PASSWORD";
    public const string EmailAlreadyRegistered = "EMAIL_ALREADY_REGISTERED";
    public const string InvalidToken = "INVALID_TOKEN";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string UserDeactivated = "USER_DEACTIVATED";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string InvalidGoogleToken = "INVALID_GOOGLE_TOKEN";
    public const string GoogleEmailNotVerified = "GOOGLE_EMAIL_NOT_VERIFIED";
    public const string EmailExistsUnverified = "EMAIL_EXISTS_UNVERIFIED";
    public const string GoogleLoginDisabled = "GOOGLE_LOGIN_DISABLED";
    public const string InvalidMicrosoftToken = "INVALID_MICROSOFT_TOKEN";
    public const string MicrosoftLoginDisabled = "MICROSOFT_LOGIN_DISABLED";
    public const string InvalidFacebookToken = "INVALID_FACEBOOK_TOKEN";
    public const string FacebookEmailRequired = "FACEBOOK_EMAIL_REQUIRED";
    public const string FacebookLoginDisabled = "FACEBOOK_LOGIN_DISABLED";
}
