using Blog.Domain.Common;

namespace Blog.Domain.DomainEvents;

public record CommentAddedEvent(Guid CommentId, Guid PostId) : IDomainEvent;
public record CommentApprovedEvent(Guid CommentId) : IDomainEvent;
public record CommentRejectedEvent(Guid CommentId) : IDomainEvent;
public record CommentDeletedEvent(Guid CommentId, Guid PostId) : IDomainEvent;
