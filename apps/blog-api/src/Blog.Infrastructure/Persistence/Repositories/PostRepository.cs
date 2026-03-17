using Blog.Domain.Aggregates.Posts;
using Blog.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Blog.Infrastructure.Persistence.Repositories;

public class PostRepository(BlogDbContext context) : IPostRepository
{
    public async Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Posts
            .Include(p => p.Content)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Post?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await context.Posts
            .Include(p => p.Content)
            .FirstOrDefaultAsync(p => p.Slug.Value == slug, ct);

    public async Task<(IReadOnlyList<Post> Items, int TotalCount)> GetPublishedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Posts
            .AsNoTracking()
            .Where(p => p.Status == PostStatus.Published)
            .OrderByDescending(p => p.PublishedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Post post, CancellationToken ct = default)
        => await context.Posts.AddAsync(post, ct);

    public Task UpdateAsync(Post post, CancellationToken ct = default)
    {
        context.Posts.Update(post);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Post post, CancellationToken ct = default)
    {
        context.Posts.Remove(post);
        return Task.CompletedTask;
    }
}
