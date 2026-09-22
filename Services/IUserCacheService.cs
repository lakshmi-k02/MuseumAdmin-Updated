using System;
using System.Threading.Tasks;

namespace MuseumAdmin.Services
{
    public interface IUserCacheService
    {
        Task<T?> GetAsync<T>(string userId, string key);
        Task SetAsync<T>(string userId, string key, T value, TimeSpan? absoluteExpiration = null);
        Task RemoveAsync(string userId, string key);
    }
}
