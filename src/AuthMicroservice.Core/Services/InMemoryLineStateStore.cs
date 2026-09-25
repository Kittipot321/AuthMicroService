using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace AuthMicroservice.Core.Services;

internal sealed class InMemoryLineStateStore : ILineStateStore
{
    private const string KeyPrefix = "line:state:";
    private readonly IMemoryCache _cache;

    public InMemoryLineStateStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Save(string state, LineAuthState value, TimeSpan ttl)
    {
        _cache.Set(KeyPrefix + state, value, ttl);
    }

    public LineAuthState? Consume(string state)
    {
        var key = KeyPrefix + state;
        if (!_cache.TryGetValue(key, out LineAuthState? value) || value is null)
        {
            return null;
        }
        _cache.Remove(key);
        return value;
    }
}
