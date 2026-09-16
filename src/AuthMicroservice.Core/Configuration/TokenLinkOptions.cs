namespace AuthMicroservice.Core.Configuration;

public sealed class TokenLinkOptions
{
    public string EmailVerificationBaseUrl { get; set; } = "https://app.example.com/verify-email";

    public string PasswordResetBaseUrl { get; set; } = "https://app.example.com/reset-password";
}
