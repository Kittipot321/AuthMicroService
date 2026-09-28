using AuthMicroservice.Core.Contracts.Common;

namespace AuthMicroservice.Core.Services.Abstractions;

public interface IRecoveryCodeService
{
    Task<AuthResult<IReadOnlyList<string>>> GenerateAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult> VerifyAsync(Guid userId, string code, CancellationToken cancellationToken = default);

    Task<int> CountRemainingAsync(Guid userId, CancellationToken cancellationToken = default);
}
