namespace Blog.Infrastructure.Caching;

/// <summary>
/// Centralised cache key definitions. All cache keys and wildcard patterns
/// for invalidation are defined here to avoid magic strings across the codebase.
/// See caching.md for the full key convention table and TTL guidelines.
/// </summary>
public static class CacheKeys
{
    // Tags
    public static string TagList() => "tag:list:all";

    // Posts
    public static string PostBySlug(string slug) => $"post:slug:{slug}";
    public static string PostById(Guid id) => $"post:id:{id}";
    public static string PostListByPage(int page, int size) => $"post-list:page:{page}:size:{size}";
    public static string PostListByTag(string tagSlug, int page) => $"post-list:tag:{tagSlug}:{page}";
    public static string PostListByAuthor(Guid authorId, int page) => $"post-list:author:{authorId}:{page}";

    // Users
    public static string UserProfile(string username) => $"user:profile:{username}";

    // Comments
    public static string CommentsByPost(Guid postId, int page) => $"comments:post:{postId}:{page}";

    /// <summary>
    /// Wildcard patterns used by Domain Event handlers for cache invalidation.
    /// Passed to IRedisCacheService.RemoveByPatternAsync(pattern).
    /// </summary>
    public static class Patterns
    {
        public const string TagList = "tag:list:*";
        public const string AllPosts = "post:*";
        public const string PostList = "post-list:*";
        public static string PostBySlug(string slug) => $"post:slug:{slug}";
        public static string PostById(Guid id) => $"post:id:{id}";
        public static string CommentsByPost(Guid postId) => $"comments:post:{postId}:*";
        public static string UserProfile(string username) => $"user:profile:{username}";
    }
}
