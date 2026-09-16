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
        var body = Render(VerifyTemplate.Value, user, link);
        return _sender.SendAsync(user.Email ?? string.Empty, _emailOptions.Templates.VerifyEmailSubject, body, cancellationToken);
    }

    public Task SendPasswordResetAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default)
    {
        var link = BuildLink(_linkOptions.PasswordResetBaseUrl, ("email", user.Email ?? string.Empty), ("token", token));
        var body = Render(ResetTemplate.Value, user, link);
        return _sender.SendAsync(user.Email ?? string.Empty, _emailOptions.Templates.PasswordResetSubject, body, cancellationToken);
    }

    private static string Render(string template, ApplicationUser user, string link)
    {
        var name = !string.IsNullOrWhiteSpace(user.FullName)
            ? user.FullName!
            : (user.Email ?? "there");

        return template
            .Replace("{{UserName}}", System.Net.WebUtility.HtmlEncode(name), StringComparison.Ordinal)
            .Replace("{{Link}}", link, StringComparison.Ordinal);
    }

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
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"Embedded email template '{fileName}' was not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
