namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class TotpDisableRequest
{
    public string Password { get; set; } = string.Empty;
}
