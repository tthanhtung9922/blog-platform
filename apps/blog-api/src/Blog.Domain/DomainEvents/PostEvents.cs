using Blog.Domain.Common;

namespace Blog.Domain.DomainEvents;

public record PostCreatedEvent(Guid PostId) : IDomainEvent;
public record PostPublishedEvent(Guid PostId) : IDomainEvent;
public record PostUpdatedEvent(Guid PostId) : IDomainEvent;
public record PostArchivedEvent(Guid PostId) : IDomainEvent;
