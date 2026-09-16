using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Services.Internal;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.HealthChecks;

internal sealed class SmtpHealthCheck : IHealthCheck
{
    public const string Name = "auth-smtp";

    private readonly EmailOptions _emailOptions;
    private readonly int _timeoutSeconds;

    public SmtpHealthCheck(IOptions<AuthMicroserviceOptions> options)
    {
        var value = options.Value;
        _emailOptions = value.Email;
        _timeoutSeconds = value.HealthChecks.SmtpTimeoutSeconds > 0
            ? value.HealthChecks.SmtpTimeoutSeconds
            : 3;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_emailOptions.Enabled)
        {
            return HealthCheckResult.Healthy("Email is disabled — SMTP check skipped.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        try
        {
            using var client = new SmtpClient();
            var secure = SmtpConnectionHelper.ResolveSecureSocketOption(_emailOptions.Smtp);
            await client.ConnectAsync(_emailOptions.Smtp.Host, _emailOptions.Smtp.Port, secure, timeoutCts.Token).ConfigureAwait(false);
            await client.DisconnectAsync(true, timeoutCts.Token).ConfigureAwait(false);
            return HealthCheckResult.Healthy($"SMTP {_emailOptions.Smtp.Host}:{_emailOptions.Smtp.Port} is reachable.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy($"SMTP connection timed out after {_timeoutSeconds}s.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SMTP connectivity check failed.", ex);
        }
    }
}
