using Blog.Domain.Aggregates.Tags;
using Blog.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Blog.Infrastructure.Persistence.Repositories;

public class TagRepository(BlogDbContext context) : ITagRepository
{
    public async Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<Tag?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await context.Tags.FirstOrDefaultAsync(t => t.Slug.Value == slug, ct);

    public async Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct = default)
        => await context.Tags.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);

    public async Task AddAsync(Tag tag, CancellationToken ct = default)
        => await context.Tags.AddAsync(tag, ct);

    public Task UpdateAsync(Tag tag, CancellationToken ct = default)
    {
        context.Tags.Update(tag);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Tag tag, CancellationToken ct = default)
    {
        context.Tags.Remove(tag);
        return Task.CompletedTask;
    }
}
