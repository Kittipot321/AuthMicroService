namespace AuthMicroservice.Core.Contracts.Requests;

public sealed class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Role { get; set; }
}
