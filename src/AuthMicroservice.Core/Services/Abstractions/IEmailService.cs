using AuthMicroservice.Core.Domain;

namespace AuthMicroservice.Core.Services.Abstractions;

public interface IEmailService
{
    Task SendEmailVerificationAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default);
}
