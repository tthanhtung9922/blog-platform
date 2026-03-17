namespace Blog.Application.Abstractions;
public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan? CacheDuration { get; }  // null = use default TTL (5 min)
}
