using Blog.Domain.Common;

namespace Blog.Domain.DomainEvents;

public record UserProfileUpdatedEvent(Guid UserId) : IDomainEvent;
public record UserBannedEvent(Guid UserId) : IDomainEvent;
