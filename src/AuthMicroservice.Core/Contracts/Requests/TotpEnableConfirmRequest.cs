namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class TotpEnableConfirmRequest
{
    public string Code { get; set; } = string.Empty;
}
