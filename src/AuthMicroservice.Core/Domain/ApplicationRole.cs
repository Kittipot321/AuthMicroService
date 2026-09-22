using Microsoft.AspNetCore.Identity;

namespace AuthMicroservice.Core.Domain;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName) { }

    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
