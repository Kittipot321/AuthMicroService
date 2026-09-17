namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class FacebookExternalLoginRequest
{
    public string AccessToken { get; set; } = string.Empty;
}
