using Blog.Application.Features.Tags.Commands.CreateTag;
using Blog.Application.Features.Tags.Queries.GetTagList;
using Blog.Domain.Aggregates.Tags;
using Blog.Domain.ValueObjects;
using Blog.Infrastructure.Persistence;
using Blog.IntegrationTests.Fixtures;
using Blog.IntegrationTests.Helpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Blog.IntegrationTests.Infrastructure;

[Collection("Integration")]
public class CachingTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // SC3: GetTagListQuery is served from Redis on the second call (cache hit)
    [Fact]
    public async Task GetTagListQuery_SecondCall_IsServedFromRedisCache()
    {
        // Arrange: seed a tag directly via DB (bypassing MediatR to avoid influencing cache state)
        using var setupScope = fixture.Services.CreateScope();
        var db = setupScope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var seededTag = Tag.Create("Cached Tag", Slug.Create("cached-tag"));
        await db.Tags.AddAsync(seededTag);
        await db.SaveChangesAsync();

        using var scope = fixture.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var muxer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var redisDb = muxer.GetDatabase();

        // First call: cache miss — hits database, populates Redis
        var firstResult = await mediator.Send(new GetTagListQuery());
        firstResult.Should().NotBeEmpty("first call should return the seeded tag from DB");

        // Verify Redis now has the key
        var cachedValue = await redisDb.StringGetAsync("tag:list:all");
        cachedValue.HasValue.Should().BeTrue("first call should have populated the Redis cache");

        // Prove Redis is the source by deleting the DB row — if second call hits DB it returns empty
        db.Tags.Remove(seededTag);
        await db.SaveChangesAsync();

        // Second call: must be a cache hit — DB row is gone but cache still has it
        var secondResult = await mediator.Send(new GetTagListQuery());
        secondResult.Should().NotBeEmpty(
            "second call should be served from Redis even though the DB row was deleted");
        secondResult.Should().HaveCount(firstResult.Count,
            "cached response should have the same items as the first call");
    }

    // SC4+SC5: CreateTagCommand fires TagCreatedEvent which invalidates tag:list:* via Lua script
    [Fact]
    public async Task CreateTagCommand_WhenSucceeds_InvalidatesTagListCache()
    {
        // Arrange: seed the cache with a stale value via first query call
        using var seedScope = fixture.Services.CreateScope();
        var db = seedScope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var existingTag = Tag.Create("Existing Tag", Slug.Create("existing-tag"));
        await db.Tags.AddAsync(existingTag);
        await db.SaveChangesAsync();

        using var populateScope = fixture.Services.CreateScope();
        var populateMediator = populateScope.ServiceProvider.GetRequiredService<IMediator>();
        var firstQuery = await populateMediator.Send(new GetTagListQuery());
        firstQuery.Should().HaveCount(1, "one tag seeded before cache population");

        // Verify cache is populated
        var muxer = fixture.Services.GetRequiredService<IConnectionMultiplexer>();
        var redisDb = muxer.GetDatabase();
        var preCacheValue = await redisDb.StringGetAsync("tag:list:all");
        preCacheValue.HasValue.Should().BeTrue("cache should be populated after first query");

        // Act: send CreateTagCommand via HTTP with Admin JWT (requires Admin or Editor role)
        var adminToken = JwtTokenHelper.GenerateJwt("Admin");
        fixture.HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await fixture.HttpClient.PostAsJsonAsync(
            "/api/v1/tags",
            new { Name = "New Tag After Cache" });
        response.EnsureSuccessStatusCode();

        // Assert SC4: CreateTagCommand executed successfully (response was 2xx)
        // Assert SC5: tag:list:* cache invalidated by Lua script via TagCreatedCacheInvalidationHandler
        var postCacheValue = await redisDb.StringGetAsync("tag:list:all");
        postCacheValue.HasValue.Should().BeFalse(
            "TagCreatedEvent should have fired TagCreatedCacheInvalidationHandler which runs " +
            "RemoveByPatternAsync(tag:list:*) via Lua SCAN+DEL script, clearing the cache key");

        // Final verification: querying again returns 2 tags (existing + new) from DB (cache miss)
        using var finalScope = fixture.Services.CreateScope();
        var finalMediator = finalScope.ServiceProvider.GetRequiredService<IMediator>();
        var finalResult = await finalMediator.Send(new GetTagListQuery());
        finalResult.Should().HaveCount(2,
            "after cache invalidation, next query hits DB and returns both existing and new tags");
    }
}
