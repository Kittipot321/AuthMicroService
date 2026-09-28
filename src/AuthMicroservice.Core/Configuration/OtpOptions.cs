namespace AuthMicroservice.Core.Configuration;

public sealed class OtpOptions
{
    public int CodeLength { get; set; } = 6;

    public int ExpirationMinutes { get; set; } = 10;

    public int MaxAttempts { get; set; } = 5;

    public int ResendCooldownSeconds { get; set; } = 60;

    public EmailVerificationOtpOptions EmailVerification { get; set; } = new();

    public OtpPurposeToggle LoginTwoFactor { get; set; } = new();
}

public class OtpPurposeToggle
{
    public bool Enabled { get; set; } = true;
}

public sealed class EmailVerificationOtpOptions : OtpPurposeToggle
{
    public EmailVerificationDeliveryMode Mode { get; set; } = EmailVerificationDeliveryMode.Link;
}

public enum EmailVerificationDeliveryMode
{
    Link = 0,
    Code = 1
}
