using Blog.Domain.Aggregates.Posts;
using Blog.Infrastructure.Persistence;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Blog.ArchTests;

/// <summary>
/// Enforces the layer dependency direction:
/// Domain → Application → Infrastructure → Presentation
///
/// These tests pass vacuously in Phase 1 for Application and API rules
/// (those layers are empty/minimal). They enforce boundaries as those
/// layers grow in Phase 2+.
/// </summary>
public class LayerBoundaryTests
{
    // Anchor types to locate each assembly by reflection
    private static readonly System.Reflection.Assembly DomainAssembly =
        typeof(Post).Assembly;

    private static readonly System.Reflection.Assembly InfrastructureAssembly =
        typeof(BlogDbContext).Assembly;

    [Fact]
    public void Domain_ShouldNot_ReferenceBlogInfrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Blog.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain layer must not reference Infrastructure (dependency direction rule)");
    }

    [Fact]
    public void Domain_ShouldNot_ReferenceBlogAPI()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Blog.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain layer must not reference API (dependency direction rule)");
    }

    [Fact]
    public void Infrastructure_ShouldNot_ReferenceBlogAPI()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("Blog.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Infrastructure layer must not reference API (dependency direction rule)");
    }

    [Fact]
    public void Domain_AggregatiesAndValueObjects_ShouldNot_ReferenceMediatRDirectly()
    {
        // Blog.Domain uses MediatR in exactly two namespaces:
        //   1. Blog.Domain.Common       — IDomainEvent : MediatR.INotification (the bridge interface)
        //   2. Blog.Domain.DomainEvents — domain event records implement IDomainEvent
        //
        // No aggregate, value object, repository interface, or exception class may directly
        // implement or inherit from MediatR types. This rule keeps framework coupling isolated
        // to the event infrastructure.
        //
        // We check by reflection: no type in Aggregates/ValueObjects/Repositories/Exceptions
        // should directly implement a MediatR interface or inherit from a MediatR base class.
        var outsideAllowedNamespaces = DomainAssembly
            .GetTypes()
            .Where(t => t.Namespace != null
                        && !t.Namespace.StartsWith("Blog.Domain.Common")
                        && !t.Namespace.StartsWith("Blog.Domain.DomainEvents"))
            .ToList();

        var violations = outsideAllowedNamespaces
            .Where(t =>
                t.GetInterfaces().Any(i => i.Assembly.GetName().Name?.StartsWith("MediatR") == true)
                || (t.BaseType != null && t.BaseType.Assembly.GetName().Name?.StartsWith("MediatR") == true))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        violations.Should().BeEmpty(
            because: "Aggregates, value objects, repositories, and exceptions must not implement MediatR interfaces directly. " +
                     "MediatR coupling belongs only in Blog.Domain.Common (IDomainEvent) and Blog.Domain.DomainEvents. " +
                     $"Violating types: {string.Join(", ", violations)}");
    }
}
