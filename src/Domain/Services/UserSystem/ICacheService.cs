namespace DbApp.Domain.Services.UserSystem;

/// <summary>
/// Domain service interface for cache operations.
/// Provides abstraction for caching functionality.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a value from cache.
    /// </summary>
    /// <typeparam name="T">Type of the cached value</typeparam>
    /// <param name="key">Cache key</param>
    /// <returns>Cached value or default if not found</returns>
    Task<T?> GetAsync<T>(string key) where T : class;

    /// <summary>
    /// Sets a value in cache.
    /// </summary>
    /// <typeparam name="T">Type of the value to cache</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    /// <param name="expiration">Cache expiration time</param>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;

    /// <summary>
    /// Removes a value from cache.
    /// </summary>
    /// <param name="key">Cache key</param>
    Task RemoveAsync(string key);

    /// <summary>
    /// Checks if a key exists in cache.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <returns>True if key exists</returns>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// Gets multiple values from cache.
    /// </summary>
    /// <typeparam name="T">Type of the cached values</typeparam>
    /// <param name="keys">Cache keys</param>
    /// <returns>Dictionary of key-value pairs</returns>
    Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys) where T : class;

    /// <summary>
    /// Sets multiple values in cache.
    /// </summary>
    /// <typeparam name="T">Type of the values to cache</typeparam>
    /// <param name="keyValuePairs">Key-value pairs to cache</param>
    /// <param name="expiration">Cache expiration time</param>
    Task SetManyAsync<T>(Dictionary<string, T> keyValuePairs, TimeSpan? expiration = null) where T : class;
}
