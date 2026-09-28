namespace AuthMicroservice.Core.Domain;

public class TwoFactorRecoveryCode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public string Salt { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public string? IpAddress { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public bool IsConsumed => ConsumedAt.HasValue;
}
