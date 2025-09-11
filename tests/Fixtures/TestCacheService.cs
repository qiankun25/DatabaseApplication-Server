using DbApp.Domain.Services.UserSystem;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DbApp.Tests.Fixtures;

/// <summary>
/// Test implementation of cache service using in-memory cache instead of Redis.
/// This avoids Redis connection issues during testing.
/// </summary>
public class TestCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<TestCacheService> _logger;

    public TestCacheService(IMemoryCache memoryCache, ILogger<TestCacheService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            if (_memoryCache.TryGetValue(key, out var cachedValue))
            {
                if (cachedValue is string jsonString)
                {
                    return JsonSerializer.Deserialize<T>(jsonString);
                }
                return cachedValue as T;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cached value for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        try
        {
            var options = new MemoryCacheEntryOptions();
            if (expiration.HasValue)
            {
                options.SetAbsoluteExpiration(expiration.Value);
            }
            else
            {
                // Default expiration for tests
                options.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
            }

            var jsonString = JsonSerializer.Serialize(value);
            _memoryCache.Set(key, jsonString, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cached value for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            _memoryCache.Remove(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cached value for key: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return _memoryCache.TryGetValue(key, out _);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if key exists: {Key}", key);
            return false;
        }
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? expiration = null)
    {
        try
        {
            var options = new MemoryCacheEntryOptions();
            if (expiration.HasValue)
            {
                options.SetAbsoluteExpiration(expiration.Value);
            }
            else
            {
                options.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
            }

            _memoryCache.Set(key, value, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting string value for key: {Key}", key);
        }
    }

    public async Task<string?> GetStringAsync(string key)
    {
        try
        {
            return _memoryCache.TryGetValue(key, out var value) ? value?.ToString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting string value for key: {Key}", key);
            return null;
        }
    }

    public async Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys) where T : class
    {
        var result = new Dictionary<string, T?>();

        foreach (var key in keys)
        {
            result[key] = await GetAsync<T>(key);
        }

        return result;
    }

    public async Task SetManyAsync<T>(Dictionary<string, T> keyValuePairs, TimeSpan? expiration = null) where T : class
    {
        var tasks = keyValuePairs.Select(kvp => SetAsync(kvp.Key, kvp.Value, expiration));
        await Task.WhenAll(tasks);
    }
}
