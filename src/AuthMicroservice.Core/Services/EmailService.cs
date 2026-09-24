using System.Globalization;
using System.Reflection;
using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Domain;
using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Services;

internal sealed class EmailService : IEmailService
{
    private static readonly Lazy<string> VerifyTemplate = new(() => LoadTemplate("verify-email.html"));
    private static readonly Lazy<string> ResetTemplate = new(() => LoadTemplate("password-reset.html"));
    private static readonly Lazy<string> OtpEmailVerificationTemplate = new(() => LoadTemplate("otp-email-verification.html"));
    private static readonly Lazy<string> OtpLoginTwoFactorTemplate = new(() => LoadTemplate("otp-login-2fa.html"));

    private readonly IEmailSender _sender;
    private readonly EmailOptions _emailOptions;
    private readonly TokenLinkOptions _linkOptions;

    public EmailService(IEmailSender sender, IOptions<AuthMicroserviceOptions> options)
    {
        _sender = sender;
        _emailOptions = options.Value.Email;
        _linkOptions = options.Value.TokenLinks;
    }

    public Task SendEmailVerificationAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default)
    {
        var link = BuildLink(_linkOptions.EmailVerificationBaseUrl, ("userId", user.Id.ToString()), ("token", token));
        var body = RenderLink(VerifyTemplate.Value, user, link);
        return _sender.SendAsync(user.Email ?? string.Empty, _emailOptions.Templates.VerifyEmailSubject, body, cancellationToken);
    }

    public Task SendPasswordResetAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default)
    {
        var link = BuildLink(_linkOptions.PasswordResetBaseUrl, ("email", user.Email ?? string.Empty), ("token", token));
        var body = RenderLink(ResetTemplate.Value, user, link);
        return _sender.SendAsync(user.Email ?? string.Empty, _emailOptions.Templates.PasswordResetSubject, body, cancellationToken);
    }

    public Task SendOtpAsync(ApplicationUser user, string code, OtpPurpose purpose, int expiresInMinutes, CancellationToken cancellationToken = default)
    {
        var (template, subject) = purpose switch
        {
            OtpPurpose.EmailVerification => (OtpEmailVerificationTemplate.Value, _emailOptions.Templates.OtpEmailVerificationSubject),
            OtpPurpose.LoginTwoFactor => (OtpLoginTwoFactorTemplate.Value, _emailOptions.Templates.OtpLoginTwoFactorSubject),
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unsupported OTP purpose.")
        };

        var body = RenderOtp(template, user, code, expiresInMinutes);
        return _sender.SendAsync(user.Email ?? string.Empty, subject, body, cancellationToken);
    }

    private static string RenderLink(string template, ApplicationUser user, string link)
    {
        var name = ResolveDisplayName(user);
        return template
            .Replace("{{UserName}}", System.Net.WebUtility.HtmlEncode(name), StringComparison.Ordinal)
            .Replace("{{Link}}", link, StringComparison.Ordinal);
    }

    private static string RenderOtp(string template, ApplicationUser user, string code, int expiresInMinutes)
    {
        var name = ResolveDisplayName(user);
        return template
            .Replace("{{UserName}}", System.Net.WebUtility.HtmlEncode(name), StringComparison.Ordinal)
            .Replace("{{Code}}", System.Net.WebUtility.HtmlEncode(code), StringComparison.Ordinal)
            .Replace("{{ExpiresInMinutes}}", expiresInMinutes.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static string ResolveDisplayName(ApplicationUser user)
        => !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName! : (user.Email ?? "there");

    private static string BuildLink(string baseUrl, params (string Key, string Value)[] queryParams)
    {
        var separator = baseUrl.Contains('?') ? '&' : '?';
        var pairs = queryParams.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}");
        return $"{baseUrl}{separator}{string.Join("&", pairs)}";
    }

    private static string LoadTemplate(string fileName)
    {
        var assembly = typeof(EmailService).GetTypeInfo().Assembly;
        var suffix = "." + fileName;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"Embedded email template '{fileName}' was not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
