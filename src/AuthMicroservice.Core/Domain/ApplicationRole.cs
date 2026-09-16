using Microsoft.AspNetCore.Identity;

namespace AuthMicroservice.Core.Domain;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName) { }
}
