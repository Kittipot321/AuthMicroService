namespace AuthMicroservice.Core.Domain;

public class OtpCode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public string Salt { get; set; } = string.Empty;

    public OtpPurpose Purpose { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public int Attempts { get; set; }

    public string? IpAddress { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public bool IsConsumed => ConsumedAt.HasValue;

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;

    public bool IsActive(DateTime utcNow) => !IsConsumed && !IsExpired(utcNow);
}
