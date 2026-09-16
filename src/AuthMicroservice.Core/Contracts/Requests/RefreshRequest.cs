namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class RefreshRequest
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
