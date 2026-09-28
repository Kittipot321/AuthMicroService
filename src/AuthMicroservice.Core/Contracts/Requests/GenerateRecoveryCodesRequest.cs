namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class GenerateRecoveryCodesRequest
{
    public string Password { get; set; } = string.Empty;
}
