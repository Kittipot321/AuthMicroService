using AuthMicroservice.Core.Configuration;

namespace AuthMicroservice.Core.Contracts.Responses;

public sealed class EmailVerificationRequiredResponse
{
    public string Email { get; set; } = string.Empty;
    public EmailVerificationMode Mode { get; set; } = EmailVerificationMode.Link;
    public string Message { get; set; } = "Email verification required.";
    public bool VerificationSent { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
