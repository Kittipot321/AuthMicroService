namespace AuthMicroservice.Core.Contracts.Responses;

public sealed class RecoveryCodesResponse
{
    public IReadOnlyList<string> Codes { get; set; } = Array.Empty<string>();

    public DateTime GeneratedAt { get; set; }
}
