using Microsoft.AspNetCore.Identity;

namespace AuthMicroservice.Core.Domain;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public bool IsDeactivated { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
