using Blog.Domain.Common;
using Blog.Domain.DomainEvents;
using Blog.Domain.Exceptions;
using Blog.Domain.ValueObjects;

namespace Blog.Domain.Aggregates.Posts;

public class Post : AggregateRoot<Guid>
{
    public Guid AuthorId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public Slug Slug { get; private set; } = null!;
    public string? Excerpt { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public PostStatus Status { get; private set; }
    public bool IsFeatured { get; private set; }
    public ReadingTime? ReadingTime { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PostContent? _content;
    public PostContent? Content => _content;

    private readonly List<PostVersion> _versions = new();
    public IReadOnlyList<PostVersion> Versions => _versions.AsReadOnly();

    private readonly List<TagReference> _tags = new();
    public IReadOnlyList<TagReference> Tags => _tags.AsReadOnly();

    private Post() { }  // EF Core materializer

    public static Post Create(Guid authorId, string title, Slug slug)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            Title = title,
            Slug = slug,
            Status = PostStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        post.AddDomainEvent(new PostCreatedEvent(post.Id));
        return post;
    }

    public void UpdateDetails(string title, Slug slug, string? excerpt, string? coverImageUrl)
    {
        Title = title;
        Slug = slug;
        Excerpt = excerpt;
        CoverImageUrl = coverImageUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new PostUpdatedEvent(Id));
    }

    public void SetContent(string bodyJson, string bodyHtml, int wordCount)
    {
        if (_content is null)
            _content = PostContent.Create(Id, bodyJson, bodyHtml);
        else
            _content.Update(bodyJson, bodyHtml);

        ReadingTime = ReadingTime.FromWordCount(wordCount);
        var versionNumber = _versions.Count + 1;
        _versions.Add(PostVersion.Create(Id, bodyJson, versionNumber));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Publish()
    {
        if (Status != PostStatus.Draft)
            throw new DomainException($"Cannot publish a post with status '{Status}'. Only Draft posts can be published.");

        Status = PostStatus.Published;
        PublishedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new PostPublishedEvent(Id));
    }

    public void Archive()
    {
        if (Status == PostStatus.Archived)
            throw new DomainException("Post is already archived.");

        Status = PostStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new PostArchivedEvent(Id));
    }

    public void SetFeatured(bool isFeatured)
    {
        IsFeatured = isFeatured;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddTag(TagReference tag)
    {
        if (!_tags.Any(t => t.TagId == tag.TagId))
            _tags.Add(tag);
    }

    public void RemoveTag(Guid tagId)
    {
        var tag = _tags.FirstOrDefault(t => t.TagId == tagId);
        if (tag is not null) _tags.Remove(tag);
    }
}
