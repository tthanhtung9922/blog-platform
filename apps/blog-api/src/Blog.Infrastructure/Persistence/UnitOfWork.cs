using Blog.Application.Abstractions;
using Blog.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Blog.Infrastructure.Persistence;

public class UnitOfWork(
    BlogDbContext blogContext,
    IPublisher publisher,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken ct = default)
    {
        // 1. Collect domain events from all change-tracked AggregateRoot<Guid> entities.
        // NOTE: All current aggregates (Tag, Post, Comment, User) use AggregateRoot<Guid>.
        // If a future aggregate uses a non-Guid key type, this scan will miss it.
        var aggregates = blogContext.ChangeTracker
            .Entries<AggregateRoot<Guid>>()
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        var events = aggregates
            .SelectMany(a => a.DomainEvents)
            .ToList();

        // 2. Clear domain events before save (prevents double-dispatch on retry).
        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();

        // 3. Persist to database.
        await blogContext.SaveChangesAsync(ct);

        // 4. Dispatch events after successful persist.
        // Failures are logged and swallowed — primary write already committed.
        // Side effects (cache invalidation, emails) are fire-and-forget.
        foreach (var evt in events)
        {
            try
            {
                await publisher.Publish(evt, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Domain event handler failed after commit for event {EventType}. " +
                    "Primary write succeeded. Consider outbox pattern for at-least-once delivery.",
                    evt.GetType().Name);
            }
        }
    }
}
