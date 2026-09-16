namespace AuthMicroservice.Core.Services.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
