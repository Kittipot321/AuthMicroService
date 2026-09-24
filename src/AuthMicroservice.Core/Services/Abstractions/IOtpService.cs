using AuthMicroservice.Core.Contracts.Common;
using AuthMicroservice.Core.Domain;

namespace AuthMicroservice.Core.Services.Abstractions;

public interface IOtpService
{
    Task<AuthResult<string>> GenerateAsync(Guid userId, OtpPurpose purpose, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult> VerifyAsync(Guid userId, OtpPurpose purpose, string code, CancellationToken cancellationToken = default);
}
