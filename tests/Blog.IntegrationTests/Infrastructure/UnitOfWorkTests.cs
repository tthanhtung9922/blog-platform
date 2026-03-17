using Blog.Application.Abstractions;
using Blog.Domain.Aggregates.Tags;
using Blog.Domain.ValueObjects;
using Blog.Infrastructure.Persistence;
using Blog.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Blog.IntegrationTests.Infrastructure;

[Collection("Integration")]
public class UnitOfWorkTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CommitAsync_WhenSaveSucceeds_PersistsChanges()
    {
        // Arrange
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var tag = Tag.Create("Integration Test Tag", Slug.Create("Integration Test Tag"));
        await db.Tags.AddAsync(tag);

        // Act
        await uow.CommitAsync();

        // Assert: tag persisted in a fresh scope (no EF change-tracking cache influence)
        using var verifyScope = fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var persisted = await verifyDb.Tags.FirstOrDefaultAsync(t => t.Id == tag.Id);
        persisted.Should().NotBeNull("tag should be persisted after CommitAsync");
        persisted!.Name.Should().Be("Integration Test Tag");
    }

    [Fact]
    public async Task CommitAsync_WhenDatabaseConstraintFails_RollsBackChanges()
    {
        // Arrange: create a tag first to establish the unique slug
        using var setupScope = fixture.Services.CreateScope();
        var setupDb = setupScope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var setupUow = setupScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var firstTag = Tag.Create("Unique Tag", Slug.Create("unique-tag"));
        await setupDb.Tags.AddAsync(firstTag);
        await setupUow.CommitAsync();

        // Act: try to create a tag with the same slug (unique constraint violation)
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var duplicateTag = Tag.Create("Unique Tag", Slug.Create("unique-tag"));
        await db.Tags.AddAsync(duplicateTag);

        Func<Task> act = async () => await uow.CommitAsync();

        // Assert: CommitAsync throws (DB constraint violation) and nothing new is persisted
        await act.Should().ThrowAsync<Exception>("duplicate slug violates unique constraint");

        using var verifyScope = fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var count = await verifyDb.Tags.CountAsync(t => t.Slug.Value == "unique-tag");
        count.Should().Be(1, "only the first tag should persist; the duplicate should be rolled back");
    }
}
