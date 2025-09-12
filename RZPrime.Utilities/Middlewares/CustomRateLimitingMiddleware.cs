using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using RZPrime.Utilities.Extension;
using RZPrime.Utilities.Attributes;
using RZPrime.Utilities.Exceptions;

namespace RZPrime.Utilities.Middlewares
{
    public class CustomRateLimitingMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        public async Task Invoke(HttpContext context)
        {
            var rateLimitAttribute = context.GetEndpoint()?.Metadata.GetMetadata<CustomRateLimitAttribute>();

            if (rateLimitAttribute != null)
            {
                var identifier = context.GetClaim("Publickey") ?? context.GetRequestIpv4().Split(",")[0];
                var cacheKey = $"{identifier}_login_attempts";

                if (cache.TryGetValue(cacheKey, out int attempts))
                {
                    if (attempts >= rateLimitAttribute.MaxAttemptsCount)
                    {
                        throw new TooManyRequestsException(rateLimitAttribute.Message);
                    }
                }

                attempts = cache.TryGetValue(cacheKey, out int existingAttempts) ? existingAttempts + 1 : 1;
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(rateLimitAttribute.LockoutDurationMinutes)
                };
                cache.Set(cacheKey, attempts, cacheEntryOptions);
            }

            await next(context);
        }
    }
}
