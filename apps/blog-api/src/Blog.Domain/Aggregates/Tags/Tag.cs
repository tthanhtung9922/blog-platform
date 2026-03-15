using Blog.Domain.Common;
using Blog.Domain.DomainEvents;
using Blog.Domain.ValueObjects;

namespace Blog.Domain.Aggregates.Tags;

public class Tag : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public Slug Slug { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private Tag() { }

    public static Tag Create(string name, Slug slug)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name cannot be empty.", nameof(name));

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            CreatedAt = DateTimeOffset.UtcNow
        };

        tag.AddDomainEvent(new TagCreatedEvent(tag.Id));
        return tag;
    }

    public void Update(string name, Slug slug)
    {
        Name = name;
        Slug = slug;
    }
}
