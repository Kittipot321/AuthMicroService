namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class Enable2FaConfirmRequest
{
    public string Code { get; set; } = string.Empty;
}
