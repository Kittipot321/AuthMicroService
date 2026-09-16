namespace AuthMicroservice.Core.Contracts.Responses;

public sealed class UserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public bool EmailConfirmed { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>();
}
