namespace Blog.Application.Abstractions;
/// <summary>
/// Wraps BlogDbContext save + domain event dispatch as a single explicit operation.
/// Phase 2: BlogDbContext only. Phase 3 adds cross-context (IdentityDbContext) overload.
/// </summary>
public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken ct = default);
}
