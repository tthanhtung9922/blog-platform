using Blog.Domain.Aggregates.Posts;
using Blog.Domain.Common;
using FluentAssertions;
using NetArchTest.Rules;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Blog.ArchTests;

/// <summary>
/// Enforces domain model structural rules:
/// - Value objects are immutable (no public setters)
/// - Domain events are C# record types
/// - Aggregate roots inherit from AggregateRoot&lt;TId&gt;
///
/// These rules prevent common DDD pattern violations that are easy to
/// introduce accidentally and hard to detect in code review.
/// </summary>
public class DomainModelIntegrityTests
{
    private static readonly Assembly DomainAssembly = typeof(Post).Assembly;

    [Fact]
    public void ValueObjects_ShouldBe_Immutable()
    {
        // All classes in Blog.Domain.ValueObjects must have no public property setters.
        // NetArchTest checks this via reflection — any `public set;` on a property is a violation.
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("Blog.Domain.ValueObjects")
            .And()
            .AreClasses()
            .Should()
            .BeImmutable()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Value objects must be immutable — all properties must use { get; } with no public setter");
    }

    [Fact]
    public void ValueObjects_ShouldInherit_ValueObjectBase()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("Blog.Domain.ValueObjects")
            .And()
            .AreClasses()
            .Should()
            .Inherit(typeof(ValueObject))
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "All classes in Blog.Domain.ValueObjects must inherit from ValueObject base class for structural equality");
    }

    [Fact]
    public void DomainEvents_ShouldBe_RecordTypes()
    {
        // Record types in C# are classes with IsRecord metadata set.
        // We verify this via reflection on types in Blog.Domain.DomainEvents namespace.
        var domainEventTypes = DomainAssembly
            .GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("Blog.Domain.DomainEvents"))
            .Where(t => t.IsClass && !t.IsAbstract)
            .ToList();

        domainEventTypes.Should().NotBeEmpty(
            because: "Blog.Domain.DomainEvents namespace must contain domain event types");

        foreach (var eventType in domainEventTypes)
        {
            // C# records have a compiler-generated "EqualityContract" property
            var isRecord = eventType.GetProperty("EqualityContract",
                BindingFlags.NonPublic | BindingFlags.Instance) != null;

            isRecord.Should().BeTrue(
                because: $"{eventType.Name} must be a `record` type (not a class) — records are immutable by design and communicate past-tense facts");
        }
    }

    [Fact]
    public void AggregateRoots_ShouldInherit_AggregateRootBase()
    {
        // All concrete classes in Blog.Domain.Aggregates.* sub-namespaces that
        // are intended as aggregate roots (not child entities like PostContent) should
        // inherit from AggregateRoot<TId>. We check for types that are direct children
        // of their aggregate's namespace folder (Post, Comment, User, Tag).
        var aggregateRootTypes = new[]
        {
            typeof(Blog.Domain.Aggregates.Posts.Post),
            typeof(Blog.Domain.Aggregates.Comments.Comment),
            typeof(Blog.Domain.Aggregates.Users.User),
            typeof(Blog.Domain.Aggregates.Tags.Tag),
        };

        foreach (var aggregateType in aggregateRootTypes)
        {
            var inheritsAggregateRoot = InheritsFromGenericAggregateRoot(aggregateType);

            inheritsAggregateRoot.Should().BeTrue(
                because: $"{aggregateType.Name} must inherit from AggregateRoot<TId> to participate in the domain events lifecycle");
        }
    }

    [Fact]
    public void DomainEvents_ShouldImplement_IDomainEvent()
    {
        var domainEventTypes = DomainAssembly
            .GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("Blog.Domain.DomainEvents"))
            .Where(t => t.IsClass && !t.IsAbstract)
            .ToList();

        foreach (var eventType in domainEventTypes)
        {
            var implementsIDomainEvent = typeof(IDomainEvent).IsAssignableFrom(eventType);

            implementsIDomainEvent.Should().BeTrue(
                because: $"{eventType.Name} must implement IDomainEvent to be dispatched by MediatR");
        }
    }

    private static bool InheritsFromGenericAggregateRoot(System.Type type)
    {
        var current = type.BaseType;
        while (current != null && current != typeof(object))
        {
            if (current.IsGenericType &&
                current.GetGenericTypeDefinition() == typeof(AggregateRoot<>))
                return true;
            current = current.BaseType;
        }
        return false;
    }
}
