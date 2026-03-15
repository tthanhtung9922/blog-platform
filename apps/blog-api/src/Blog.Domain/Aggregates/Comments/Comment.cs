using Blog.Domain.Common;
using Blog.Domain.DomainEvents;
using Blog.Domain.Exceptions;

namespace Blog.Domain.Aggregates.Comments;

public class Comment : AggregateRoot<Guid>
{
    public Guid PostId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }   // null = top-level, set = 1st-level reply
    public CommentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Comment() { }

    public static Comment Create(Guid postId, Guid authorId, string body, Guid? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Comment body cannot be empty.", nameof(body));
        if (body.Length > 5000)
            throw new DomainException("Comment body cannot exceed 5000 characters.");

        return new Comment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            AuthorId = authorId,
            Body = body,
            ParentId = parentId,
            Status = CommentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates a reply to this comment. DOMAIN RULE: replies cannot be nested
    /// more than one level deep. If this comment is already a reply (has a ParentId),
    /// this method throws DomainException.
    /// </summary>
    public Comment AddReply(Guid authorId, string body)
    {
        if (ParentId.HasValue)
            throw new DomainException("Cannot nest replies more than one level deep.");

        var reply = Create(PostId, authorId, body, parentId: Id);
        AddDomainEvent(new CommentAddedEvent(reply.Id, PostId));
        return reply;
    }

    public void Approve()
    {
        Status = CommentStatus.Approved;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new CommentApprovedEvent(Id));
    }

    public void Reject()
    {
        Status = CommentStatus.Rejected;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new CommentRejectedEvent(Id));
    }

    public void Delete()
    {
        AddDomainEvent(new CommentDeletedEvent(Id, PostId));
    }
}
