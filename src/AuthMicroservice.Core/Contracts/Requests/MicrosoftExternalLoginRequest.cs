namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class MicrosoftExternalLoginRequest
{
    public string IdToken { get; set; } = string.Empty;
}
