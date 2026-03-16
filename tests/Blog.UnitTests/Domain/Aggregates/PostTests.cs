using Blog.Domain.Aggregates.Posts;
using Blog.Domain.DomainEvents;
using Blog.Domain.Exceptions;
using Blog.Domain.ValueObjects;

namespace Blog.UnitTests.Domain.Aggregates;

public class PostTests
{
    private static Slug TestSlug() => Slug.Create("my-title");

    [Fact]
    public void Create_WhenValidArgs_StatusIsDraft()
    {
        var post = Post.Create(Guid.NewGuid(), "My Title", TestSlug());
        Assert.Equal(PostStatus.Draft, post.Status);
        Assert.NotEqual(Guid.Empty, post.Id);
    }

    [Fact]
    public void Publish_WhenDraft_StatusIsPublished()
    {
        var post = Post.Create(Guid.NewGuid(), "My Title", TestSlug());
        post.ClearDomainEvents();

        post.Publish();

        Assert.Equal(PostStatus.Published, post.Status);
        Assert.NotNull(post.PublishedAt);
        Assert.Contains(post.DomainEvents, e => e is PostPublishedEvent);
    }

    [Fact]
    public void Publish_WhenAlreadyPublished_ThrowsInvalidOperationException()
    {
        var post = Post.Create(Guid.NewGuid(), "My Title", TestSlug());
        post.Publish();

        Assert.Throws<DomainException>(() => post.Publish());
    }

    [Fact]
    public void Archive_WhenPublished_StatusIsArchived()
    {
        var post = Post.Create(Guid.NewGuid(), "My Title", TestSlug());
        post.Publish();
        post.ClearDomainEvents();

        post.Archive();

        Assert.Equal(PostStatus.Archived, post.Status);
        Assert.Contains(post.DomainEvents, e => e is PostArchivedEvent);
    }

    [Fact]
    public void AddTag_WhenNewTag_AppearsInCollection()
    {
        var post = Post.Create(Guid.NewGuid(), "My Title", TestSlug());
        var tagRef = TagReference.Create(Guid.NewGuid());

        post.AddTag(tagRef);

        Assert.Single(post.Tags);
        Assert.Equal(tagRef.TagId, post.Tags[0].TagId);
    }
}
