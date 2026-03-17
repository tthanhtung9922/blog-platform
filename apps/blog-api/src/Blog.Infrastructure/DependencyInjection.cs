using Blog.Application.Abstractions;
using Blog.Domain.Repositories;
using Blog.Infrastructure.Caching;
using Blog.Infrastructure.Persistence;
using Blog.Infrastructure.Persistence.Repositories;
using Blog.Infrastructure.Services;
using Blog.Infrastructure.Services.NoOp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Blog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBlogInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Repositories: Domain interfaces → Infrastructure implementations.
        // Scoped lifetime mirrors the EF Core DbContext lifetime (one per HTTP request).
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Unit of Work: wraps BlogDbContext save + domain event dispatch.
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Redis: IConnectionMultiplexer is a singleton — expensive to create and thread-safe.
        // Never create a new ConnectionMultiplexer per request.
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(
                configuration.GetConnectionString("Redis")
                    ?? throw new InvalidOperationException(
                        "Redis connection string 'Redis' is not configured.")));
        services.AddScoped<IRedisCacheService, RedisCacheService>();

        // Application abstraction implementations.
        // IHttpContextAccessor is required for CurrentUserService to read JWT claims.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // DateTimeService is stateless — singleton is safe and avoids repeated allocations.
        services.AddSingleton<IDateTimeService, DateTimeService>();

        // NoOp stubs for Phase 2 — replaced in Phase 3 (email) and Phase 4 (storage).
        services.AddScoped<IEmailService, NoOpEmailService>();
        services.AddScoped<IStorageService, NoOpStorageService>();

        return services;
    }
}
