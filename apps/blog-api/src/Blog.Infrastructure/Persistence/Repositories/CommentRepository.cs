using Blog.Domain.Aggregates.Comments;
using Blog.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Blog.Infrastructure.Persistence.Repositories;

public class CommentRepository(BlogDbContext context) : ICommentRepository
{
    public async Task<Comment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Comments.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Comment>> GetByPostIdAsync(Guid postId, CancellationToken ct = default)
        => await context.Comments
            .AsNoTracking()
            .Where(c => c.PostId == postId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Comment comment, CancellationToken ct = default)
        => await context.Comments.AddAsync(comment, ct);

    public Task UpdateAsync(Comment comment, CancellationToken ct = default)
    {
        context.Comments.Update(comment);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Comment comment, CancellationToken ct = default)
    {
        context.Comments.Remove(comment);
        return Task.CompletedTask;
    }
}
