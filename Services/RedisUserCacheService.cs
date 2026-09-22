using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace MuseumAdmin.Services
{
    public class RedisUserCacheService : IUserCacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisUserCacheService> _logger;
        private readonly DistributedCacheEntryOptions _options;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public RedisUserCacheService(IDistributedCache cache, ILogger<RedisUserCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
            _options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
        }

        private string KeyFor(string userId, string key) => $"user:{userId}:{key}";

        public async Task<T?> GetAsync<T>(string userId, string key)
        {
            try
            {
                var bytes = await _cache.GetAsync(KeyFor(userId, key));
                if (bytes == null || bytes.Length == 0) return default;
                return JsonSerializer.Deserialize<T>(bytes, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis get error");
                return default;
            }
        }

        public async Task SetAsync<T>(string userId, string key, T value, TimeSpan? absoluteExpiration = null)
        {
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(value, _jsonOptions);
                var opts = _options;
                if (absoluteExpiration.HasValue)
                {
                    opts = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = absoluteExpiration };
                }
                await _cache.SetAsync(KeyFor(userId, key), bytes, opts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis set error");
            }
        }

        public async Task RemoveAsync(string userId, string key)
        {
            try
            {
                await _cache.RemoveAsync(KeyFor(userId, key));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis remove error");
            }
        }
    }
}

