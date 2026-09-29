namespace AuthMicroservice.Core.Configuration;

public sealed class TokenLinkOptions
{
    public string PasswordResetBaseUrl { get; set; } = "https://app.example.com/reset-password";
}
