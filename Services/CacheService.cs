using Microsoft.Extensions.Caching.Distributed;

public class CacheService
{
    private IDistributedCache _cache;
    private ILogger<CacheService> _logger;

    public CacheService(IDistributedCache cache, ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task SetValue(string key, string value, TimeSpan? expitarionTime = null)
    {
        await _cache.SetStringAsync(key, value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expitarionTime
        });
    }

    public async Task<string?> GetValue(string key)
    {
        return await _cache.GetStringAsync(key);
    }
}