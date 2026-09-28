using Microsoft.AspNetCore.Identity;

namespace AuthMicroservice.Core.Domain;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public bool IsDeactivated { get; set; }

    public bool EmailTwoFactorEnabled { get; set; }

    public bool TotpEnabled { get; set; }

    public string? TotpSecretProtected { get; set; }

    public DateTime? TotpConfirmedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public ICollection<TwoFactorRecoveryCode> RecoveryCodes { get; set; } = new List<TwoFactorRecoveryCode>();
}
