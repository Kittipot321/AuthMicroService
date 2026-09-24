namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class SendEmailVerificationOtpRequest
{
    public string Email { get; set; } = string.Empty;
}
