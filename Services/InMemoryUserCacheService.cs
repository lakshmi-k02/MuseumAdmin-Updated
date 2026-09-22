using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading.Tasks;

namespace MuseumAdmin.Services
{
    public class InMemoryUserCacheService : IUserCacheService
    {
        private readonly IMemoryCache _cache;

        public InMemoryUserCacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        private string MakeKey(string userId, string key) => $"user:{userId}:{key}";

        public Task<T?> GetAsync<T>(string userId, string key)
        {
            var k = MakeKey(userId, key);
            if (_cache.TryGetValue(k, out T value))
                return Task.FromResult<T?>(value);
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string userId, string key, T value, TimeSpan? absoluteExpiration = null)
        {
            var k = MakeKey(userId, key);
            var options = new MemoryCacheEntryOptions();
            if (absoluteExpiration.HasValue)
                options.SetAbsoluteExpiration(absoluteExpiration.Value);
            else
                options.SetSlidingExpiration(TimeSpan.FromMinutes(10));

            _cache.Set(k, value, options);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string userId, string key)
        {
            var k = MakeKey(userId, key);
            _cache.Remove(k);
            return Task.CompletedTask;
        }
    }
}
