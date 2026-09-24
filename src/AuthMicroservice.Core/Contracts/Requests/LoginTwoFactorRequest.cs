namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class LoginTwoFactorRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
