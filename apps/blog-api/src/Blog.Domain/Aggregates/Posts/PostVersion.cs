namespace Blog.Domain.Aggregates.Posts;

public class PostVersion
{
    public Guid Id { get; private set; }
    public Guid PostId { get; private set; }
    public string BodyJson { get; private set; } = string.Empty;
    public int VersionNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PostVersion() { }

    internal static PostVersion Create(Guid postId, string bodyJson, int versionNumber)
        => new()
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            BodyJson = bodyJson,
            VersionNumber = versionNumber,
            CreatedAt = DateTimeOffset.UtcNow
        };
}
