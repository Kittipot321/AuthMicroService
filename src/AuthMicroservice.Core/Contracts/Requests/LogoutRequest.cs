namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class LogoutRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
