using Blog.Application;
using Blog.Application.Abstractions;
using Blog.Application.Behaviors;
using Blog.Application.DTOs;
using Blog.Application.Features.Tags.Queries.GetTagList;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Blog.ArchTests;

public class PipelineBehaviorOrderTests
{
    [Fact]
    public void PipelineBehaviors_MustBeRegisteredInFixedOrder_ValidationLoggingAuthorizationCaching()
    {
        // Arrange: build a minimal service collection with just the behaviors registered.
        // We are testing registration order only — not behavior execution logic.
        var services = new ServiceCollection();
        services.AddLogging();

        // AddBlogApplication() calls AddMediatR() with AddOpenBehavior() in the fixed order:
        // ValidationBehavior → LoggingBehavior → AuthorizationBehavior → CachingBehavior
        services.AddBlogApplication();

        // Register stubs to satisfy constructor injection for AuthorizationBehavior and CachingBehavior.
        // These are null — we never call Handle(), only inspect DI registration order.
        services.AddScoped<IRedisCacheService>(_ => null!);
        services.AddScoped<ICurrentUserService>(_ => null!);

        var provider = services.BuildServiceProvider();

        // Act: resolve behaviors for the probe request type (GetTagListQuery → IReadOnlyList<TagDto>).
        // MediatR resolves IEnumerable<IPipelineBehavior<TReq, TRes>> and the enumerable order
        // equals the execution order: first in list = outermost wrapper = executes first.
        var behaviors = provider
            .GetServices<IPipelineBehavior<GetTagListQuery, IReadOnlyList<TagDto>>>()
            .Select(b => b.GetType().GetGenericTypeDefinition())
            .ToList();

        // Assert: registration order must match the immutable pipeline order from CLAUDE.md ADR.
        // First registered = first in enumerable = executes first (outermost wrapper).
        var expected = new[]
        {
            typeof(ValidationBehavior<,>),
            typeof(LoggingBehavior<,>),
            typeof(AuthorizationBehavior<,>),
            typeof(CachingBehavior<,>),
        };

        behaviors.Should().Equal(expected,
            because: "MediatR pipeline behavior order is immutable: Validation → Logging → Authorization → Caching (ADR from CLAUDE.md)");
    }
}
