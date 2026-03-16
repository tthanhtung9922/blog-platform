using Blog.Domain.Aggregates.Posts;

namespace Blog.Domain.Repositories;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Post?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<(IReadOnlyList<Post> Items, int TotalCount)> GetPublishedAsync(int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Post post, CancellationToken ct = default);
    Task UpdateAsync(Post post, CancellationToken ct = default);
    Task DeleteAsync(Post post, CancellationToken ct = default);
}
