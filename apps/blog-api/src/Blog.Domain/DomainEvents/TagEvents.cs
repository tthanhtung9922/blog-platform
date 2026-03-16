using Blog.Domain.Common;

namespace Blog.Domain.DomainEvents;

public record TagCreatedEvent(Guid TagId) : IDomainEvent;
public record TagDeletedEvent(Guid TagId) : IDomainEvent;
