namespace AuthMicroservice.Core.Services.Abstractions;

public interface ICurrentUserService
{
    Guid? UserId { get; }

    string? Email { get; }

    bool IsAuthenticated { get; }

    string? IpAddress { get; }
}
