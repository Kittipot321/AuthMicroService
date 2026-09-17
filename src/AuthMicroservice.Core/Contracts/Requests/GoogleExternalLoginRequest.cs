namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class GoogleExternalLoginRequest
{
    public string IdToken { get; set; } = string.Empty;
}
