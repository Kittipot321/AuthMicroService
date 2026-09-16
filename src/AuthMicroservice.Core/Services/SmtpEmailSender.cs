using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Services.Abstractions;
using AuthMicroservice.Core.Services.Internal;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AuthMicroservice.Core.Services;

internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<AuthMicroserviceOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value.Email;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Email disabled — skipping send to {To} with subject '{Subject}'.", toEmail, subject);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();

        var secureOption = SmtpConnectionHelper.ResolveSecureSocketOption(_options.Smtp);
        await client.ConnectAsync(_options.Smtp.Host, _options.Smtp.Port, secureOption, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
        {
            await client.AuthenticateAsync(_options.Smtp.Username, _options.Smtp.Password ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
        }

        await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
    }
}
