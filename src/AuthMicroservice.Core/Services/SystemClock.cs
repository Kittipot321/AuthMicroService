using AuthMicroservice.Core.Services.Abstractions;

namespace AuthMicroservice.Core.Services;

internal sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
