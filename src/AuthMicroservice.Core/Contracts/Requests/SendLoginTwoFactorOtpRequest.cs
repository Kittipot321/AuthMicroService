namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class SendLoginTwoFactorOtpRequest
{
    public string Email { get; set; } = string.Empty;
}
