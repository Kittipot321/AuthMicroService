namespace AuthMicroservice.Core.Contracts.Responses;

public sealed class TwoFactorRequiredResponse
{
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string Message { get; set; } = "Two-factor verification required.";
}
