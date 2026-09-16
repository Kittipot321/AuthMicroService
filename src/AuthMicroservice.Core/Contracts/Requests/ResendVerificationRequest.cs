namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class ResendVerificationRequest
{
    public string Email { get; set; } = string.Empty;
}
