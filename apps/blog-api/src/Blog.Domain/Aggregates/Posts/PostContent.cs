namespace Blog.Domain.Aggregates.Posts;

public class PostContent
{
    public Guid Id { get; private set; }
    public Guid PostId { get; private set; }
    public string BodyJson { get; private set; } = string.Empty;  // Stored as JSON string; EF maps to JSONB
    public string BodyHtml { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }

    private PostContent() { }  // EF Core materializer

    internal static PostContent Create(Guid postId, string bodyJson, string bodyHtml)
        => new()
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            BodyJson = bodyJson,
            BodyHtml = bodyHtml,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    internal void Update(string bodyJson, string bodyHtml)
    {
        BodyJson = bodyJson;
        BodyHtml = bodyHtml;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
