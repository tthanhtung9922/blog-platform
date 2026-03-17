using Blog.Application.Abstractions;
using Blog.Domain.DomainEvents;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Blog.Application.Features.Tags.EventHandlers;

/// <summary>
/// Invalidates the tag list cache when a new tag is created.
/// Triggered by TagCreatedEvent dispatched from UnitOfWork.CommitAsync() after DB save.
/// Uses Lua SCAN+DEL via IRedisCacheService.RemoveByPatternAsync — never KEYS *.
/// Pattern "tag:list:*" matches all tag list cache entries (key: tag:list:all).
/// </summary>
public class TagCreatedCacheInvalidationHandler(
    IRedisCacheService cache,
    ILogger<TagCreatedCacheInvalidationHandler> logger)
    : INotificationHandler<TagCreatedEvent>
{
    // Cache key pattern for all tag list entries — matches tag:list:all and future variants
    private const string TagListPattern = "tag:list:*";

    public async Task Handle(TagCreatedEvent notification, CancellationToken ct)
    {
        logger.LogDebug("Invalidating tag cache for TagCreatedEvent {TagId}", notification.TagId);
        await cache.RemoveByPatternAsync(TagListPattern, ct);
    }
}
