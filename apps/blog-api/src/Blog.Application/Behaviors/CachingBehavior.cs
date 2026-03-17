using Blog.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;
namespace Blog.Application.Behaviors;

public class CachingBehavior<TRequest, TResponse>(
    IRedisCacheService cache,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        // Non-cacheable requests: zero overhead, no logging
        if (request is not ICacheableQuery cacheable)
            return await next();

        var cached = await cache.GetAsync<TResponse>(cacheable.CacheKey, ct);
        if (cached is not null)
        {
            logger.LogDebug("Cache HIT: {CacheKey}", cacheable.CacheKey);
            return cached;
        }

        logger.LogDebug("Cache MISS: {CacheKey}", cacheable.CacheKey);
        var result = await next();
        var ttl = cacheable.CacheDuration ?? TimeSpan.FromMinutes(5);
        await cache.SetAsync(cacheable.CacheKey, result, ttl, ct);
        return result;
    }
}
