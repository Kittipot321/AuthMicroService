using System.Collections.Concurrent;
using AuthMicroservice.Core.Services.Abstractions;

namespace AuthMicroservice.IntegrationTests.Infrastructure;

public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<CapturedEmail> _messages = new();

    public IReadOnlyCollection<CapturedEmail> Messages => _messages.ToArray();

    public void Clear() => _messages.Clear();

    public CapturedEmail? Last() => _messages.LastOrDefault();

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(new CapturedEmail(toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}
