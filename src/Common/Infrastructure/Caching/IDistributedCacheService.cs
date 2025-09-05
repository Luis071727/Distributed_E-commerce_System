public interface IDistributedCacheService
{
    Task<T> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    Task RemoveAsync(string key);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getItem, TimeSpan? expiration = null) where T : class;
}

public class RedisDistributedCacheService : IDistributedCacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisDistributedCacheService> _logger;

    public RedisDistributedCacheService(
        IDistributedCache distributedCache,
        ILogger<RedisDistributedCacheService> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(string key) where T : class
    {
        try
        {
            var json = await _distributedCache.GetStringAsync(key);
            if (json == null) return null;

            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting cache key: {key}");
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions();

            if (expiration.HasValue)
            {
                options.SetAbsoluteExpiration(expiration.Value);
            }
            else
            {
                options.SetAbsoluteExpiration(TimeSpan.FromMinutes(30)); // Default 30 minutes
            }

            await _distributedCache.SetStringAsync(key, json, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error setting cache key: {key}");
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _distributedCache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error removing cache key: {key}");
        }
    }
    //Need to continue here later
}

