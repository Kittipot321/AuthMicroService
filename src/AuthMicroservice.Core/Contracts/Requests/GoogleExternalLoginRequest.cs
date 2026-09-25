namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class GoogleExternalLoginRequest
{
    public string Code { get; set; } = string.Empty;
}
