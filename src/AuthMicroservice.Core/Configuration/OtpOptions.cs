namespace AuthMicroservice.Core.Configuration;

public sealed class OtpOptions
{
    public int CodeLength { get; set; } = 6;

    public int ExpirationMinutes { get; set; } = 10;

    public int MaxAttempts { get; set; } = 5;

    public int ResendCooldownSeconds { get; set; } = 60;

    public OtpPurposeToggle EmailVerification { get; set; } = new();

    public OtpPurposeToggle LoginTwoFactor { get; set; } = new();
}

public sealed class OtpPurposeToggle
{
    public bool Enabled { get; set; } = true;
}
