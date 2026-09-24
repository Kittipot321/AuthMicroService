namespace AuthMicroservice.Core.Configuration;

public sealed class EmailOptions
{
    public bool Enabled { get; set; } = true;

    public string FromAddress { get; set; } = "no-reply@example.com";

    public string FromName { get; set; } = "Auth Service";

    public SmtpOptions Smtp { get; set; } = new();

    public EmailTemplateOptions Templates { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 25;

    public bool UseStartTls { get; set; }

    public bool UseSsl { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }
}

public sealed class EmailTemplateOptions
{
    public string VerifyEmailSubject { get; set; } = "Verify your email";

    public string PasswordResetSubject { get; set; } = "Reset your password";

    public string OtpEmailVerificationSubject { get; set; } = "Your verification code";

    public string OtpLoginTwoFactorSubject { get; set; } = "Your login code";
}
