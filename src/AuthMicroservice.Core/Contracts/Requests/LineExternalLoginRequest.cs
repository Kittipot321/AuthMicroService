namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class LineExternalLoginRequest
{
    public string IdToken { get; set; } = string.Empty;
}
