namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}
