using Blog.Domain.Aggregates.Comments;
using Blog.Domain.Exceptions;

namespace Blog.UnitTests.Domain.Aggregates;

public class CommentTests
{
    [Fact]
    public void Create_WhenValidArgs_StatusIsPending()
    {
        var comment = Comment.Create(Guid.NewGuid(), Guid.NewGuid(), "Some body");
        Assert.Equal(CommentStatus.Pending, comment.Status);
        Assert.Null(comment.ParentId);
    }

    [Fact]
    public void AddReply_WhenTopLevelComment_ReturnsReplyWithParentId()
    {
        var comment = Comment.Create(Guid.NewGuid(), Guid.NewGuid(), "Top level");
        var reply = comment.AddReply(Guid.NewGuid(), "Reply body");

        Assert.Equal(comment.Id, reply.ParentId);
    }

    [Fact]
    public void AddReply_WhenCommentAlreadyHasParentId_ThrowsDomainException()
    {
        var topLevel = Comment.Create(Guid.NewGuid(), Guid.NewGuid(), "Top level");
        var reply = topLevel.AddReply(Guid.NewGuid(), "Reply body");

        Assert.Throws<DomainException>(() => reply.AddReply(Guid.NewGuid(), "Nested reply"));
    }
}
