namespace AuthMicroservice.Core.Contracts.Responses;

public sealed class TotpEnableConfirmResponse
{
    public IReadOnlyList<string> RecoveryCodes { get; set; } = Array.Empty<string>();
}
