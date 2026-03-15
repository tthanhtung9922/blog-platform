using Blog.Domain.Aggregates.Tags;
using Blog.Domain.DomainEvents;
using Blog.Domain.ValueObjects;

namespace Blog.UnitTests.Domain.Aggregates;

public class TagTests
{
    [Fact]
    public void Create_WhenValidArgs_TagCreatedEventRaised()
    {
        var tag = Tag.Create("csharp", Slug.Create("csharp"));
        Assert.Contains(tag.DomainEvents, e => e is TagCreatedEvent);
    }
}
