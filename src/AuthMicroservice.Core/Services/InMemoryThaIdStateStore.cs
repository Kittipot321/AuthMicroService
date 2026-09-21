using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace AuthMicroservice.Core.Services;

internal sealed class InMemoryThaIdStateStore : IThaIdStateStore
{
    private const string KeyPrefix = "thaid:state:";
    private readonly IMemoryCache _cache;

    public InMemoryThaIdStateStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Save(string state, ThaIdAuthState value, TimeSpan ttl)
    {
        _cache.Set(KeyPrefix + state, value, ttl);
    }

    public ThaIdAuthState? Consume(string state)
    {
        var key = KeyPrefix + state;
        if (!_cache.TryGetValue(key, out ThaIdAuthState? value) || value is null)
        {
            return null;
        }
        _cache.Remove(key);
        return value;
    }
}
