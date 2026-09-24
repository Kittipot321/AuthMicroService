namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class VerifyEmailOtpRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
