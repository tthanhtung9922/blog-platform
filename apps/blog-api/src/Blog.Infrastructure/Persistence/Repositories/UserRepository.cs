using Blog.Domain.Aggregates.Users;
using Blog.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Blog.Infrastructure.Persistence.Repositories;

public class UserRepository(BlogDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await context.Users.FirstOrDefaultAsync(u => u.Email.Value == email, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await context.Users.AddAsync(user, ct);

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        context.Users.Update(user);
        return Task.CompletedTask;
    }
}
