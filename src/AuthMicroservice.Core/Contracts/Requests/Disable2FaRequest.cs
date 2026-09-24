namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class Disable2FaRequest
{
    public string Password { get; set; } = string.Empty;
}
