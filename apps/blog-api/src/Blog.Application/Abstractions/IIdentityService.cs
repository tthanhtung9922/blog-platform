namespace Blog.Application.Abstractions;
/// <summary>
/// Abstraction over ASP.NET Identity operations. Defined here so IUnitOfWork can reference it
/// in Phase 3 cross-context transaction design. Concrete implementation: Phase 3.
/// </summary>
public interface IIdentityService
{
    Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken ct = default);
    Task<bool> IsEmailConfirmedAsync(Guid userId, CancellationToken ct = default);
}
