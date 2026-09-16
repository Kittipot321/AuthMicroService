using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Domain;
using AuthMicroservice.Core.Services;
using AuthMicroservice.Core.Services.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace AuthMicroservice.UnitTests.Services;

public class EmailServiceTests
{
    private static (EmailService svc, Mock<IEmailSender> sender) Build()
    {
        var sender = new Mock<IEmailSender>();
        var options = Options.Create(new AuthMicroserviceOptions
        {
            Email = new EmailOptions
            {
                Enabled = true,
                FromAddress = "no-reply@example.com",
                Templates = new EmailTemplateOptions
                {
                    VerifyEmailSubject = "Verify me",
                    PasswordResetSubject = "Reset me"
                }
            },
            TokenLinks = new TokenLinkOptions
            {
                EmailVerificationBaseUrl = "https://app.example.com/verify",
                PasswordResetBaseUrl = "https://app.example.com/reset"
            }
        });
        return (new EmailService(sender.Object, options), sender);
    }

    [Fact]
    public async Task SendEmailVerificationAsync_BuildsLinkAndInvokesSender()
    {
        var (svc, sender) = Build();
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "alice@example.com", FullName = "Alice" };

        string? capturedTo = null, capturedSubject = null, capturedBody = null;
        sender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((to, subject, body, _) =>
            {
                capturedTo = to;
                capturedSubject = subject;
                capturedBody = body;
            })
            .Returns(Task.CompletedTask);

        await svc.SendEmailVerificationAsync(user, "raw-token+with/chars");

        capturedTo.Should().Be("alice@example.com");
        capturedSubject.Should().Be("Verify me");
        capturedBody.Should().Contain("Alice");
        capturedBody.Should().Contain($"userId={user.Id}");
        capturedBody.Should().Contain("token=raw-token%2Bwith%2Fchars");
        capturedBody.Should().Contain("https://app.example.com/verify?");
    }

    [Fact]
    public async Task SendPasswordResetAsync_UsesResetTemplate()
    {
        var (svc, sender) = Build();
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "bob@example.com" };

        string? capturedBody = null;
        sender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, body, _) => capturedBody = body)
            .Returns(Task.CompletedTask);

        await svc.SendPasswordResetAsync(user, "abc123");

        capturedBody.Should().Contain("Reset your password");
        capturedBody.Should().Contain("email=bob%40example.com");
        capturedBody.Should().Contain("token=abc123");
    }
}
