using Microsoft.Extensions.Caching.Memory;

namespace OllamaConfig
{
    public class IdentityCache<T>
    {
        public IMemoryCache _cache { get; } = new MemoryCache(new MemoryCacheOptions());

        public void Add(string key, T value)
        {
            _cache.Set(key, value, DateTimeOffset.Now.AddMinutes(15));
        }

        public Either<T, CacheError> Get(string key)
        {
            var entry = _cache.Get(key);
            if (entry == null)
            {
                return new Either<T, CacheError>(CacheError.NotFound);
            }
            else
            {
                return new Either<T, CacheError>((T)entry);
            }
        }

        public void Remove(string key)
        {
            if (_cache.Get(key) != null)
            {
                _cache.Remove(key);
            }
        }
    }

    public enum CacheError
    {
        NotFound
    }
}
