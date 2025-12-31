using Microsoft.Extensions.Caching.Memory;
using RZPrime.Utilities.Services.Contracts;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Utilities.Services
{
    public class NonceService( IMemoryCache cache) : INonceService, ISingletonDependency
    {
        //private readonly HashSet<string> h = [];
        //private readonly Queue<string> q = new();

        //public bool Contains(string item) => h.Contains(item);
        //public void Add(string item)
        //{
        //    if (Contains(item))
        //        throw new BadRequestException();

        //    h.Add(item);
        //    q.Enqueue(item);

        //    if (q.Count > capacity)
        //        h.Remove(q.Dequeue());
        //}

        public bool TryUse(string nonce, TimeSpan ttl)
        {
            if (cache.TryGetValue(nonce, out _))
                return false;

            cache.Set(nonce, true, ttl);
            return true;
        }
    }
}
