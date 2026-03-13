# add-integration-test

Write an integration test using xUnit 3.2 + Testcontainers (PostgreSQL + Redis) with proper test fixture setup, database seeding, and cleanup.

## Arguments

- `feature` (required) — Feature/handler being tested (e.g., `CreatePost`, `GetPostBySlug`, `PublishPost`)
- `type` (required) — `command`, `query`, or `api` (controller-level)
- `entity` (optional) — Domain entity (e.g., `Post`, `Comment`)

## Instructions

You are writing integration tests for the blog-platform using real PostgreSQL and Redis instances via Testcontainers. These tests verify that all layers work together correctly.

### Test Project Structure

```
apps/blog-api/tests/Blog.IntegrationTests/
├── Fixtures/
│   ├── IntegrationTestFixture.cs      ← Shared Testcontainers setup
│   └── IntegrationTestCollection.cs   ← Collection definition
├── Features/
│   ├── Posts/
│   │   ├── CreatePostTests.cs
│   │   ├── GetPostBySlugTests.cs
│   │   └── PublishPostTests.cs
│   ├── Comments/
│   │   └── AddCommentTests.cs
│   └── Auth/
│       └── RegisterTests.cs
├── Helpers/
│   ├── TestDataBuilder.cs             ← Factory for test entities
│   └── AuthHelper.cs                  ← JWT token generation for tests
└── Blog.IntegrationTests.csproj
```

### Test Fixture with Testcontainers

```csharp
// Fixtures/IntegrationTestFixture.cs
public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:18")
        .WithDatabase("blog_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:8")
        .Build();

    public BlogDbContext DbContext { get; private set; } = null!;
    public IMediator Mediator { get; private set; } = null!;
    public HttpClient HttpClient { get; private set; } = null!;

    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace PostgreSQL connection
                    services.RemoveAll<DbContextOptions<BlogDbContext>>();
                    services.AddDbContext<BlogDbContext>(options =>
                        options.UseNpgsql(_postgres.GetConnectionString()));

                    // Replace Redis connection
                    services.RemoveAll<IConnectionMultiplexer>();
                    services.AddSingleton<IConnectionMultiplexer>(
                        ConnectionMultiplexer.Connect(_redis.GetConnectionString()));
                });
            });

        HttpClient = _factory.CreateClient();

        var scope = _factory.Services.CreateScope();
        DbContext = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        Mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Apply migrations
        await DbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        await _factory.DisposeAsync();
    }

    /// <summary>Reset database between tests for isolation</summary>
    public async Task ResetDatabaseAsync()
    {
        await DbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE posts, post_contents, comments, users, tags, post_tags, likes, bookmarks CASCADE;");
    }
}

// Fixtures/IntegrationTestCollection.cs
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture> { }
```

### Test Patterns

#### Command Handler Test

```csharp
[Collection("Integration")]
public class CreatePostTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public CreatePostTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreatePost_WithValidData_ReturnsPostDto()
    {
        // Arrange
        var author = TestDataBuilder.CreateUser(role: "Author");
        _fixture.DbContext.Users.Add(author);
        await _fixture.DbContext.SaveChangesAsync();

        var command = new CreatePostCommand(
            Title: "Test Post Title",
            Excerpt: "A short excerpt",
            BodyJson: TestDataBuilder.CreateTiptapJson("Hello world"),
            CoverImageUrl: null,
            TagIds: null);

        // Act
        var result = await _fixture.Mediator.Send(command);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Test Post Title", result.Title);
        Assert.Equal("test-post-title", result.Slug);
        Assert.Equal("Draft", result.Status);

        // Verify persistence
        var persisted = await _fixture.DbContext.Posts.FindAsync(result.Id);
        Assert.NotNull(persisted);
    }

    [Fact]
    public async Task CreatePost_WithEmptyTitle_ReturnsValidationError()
    {
        // Arrange
        var command = new CreatePostCommand(
            Title: "",
            Excerpt: null,
            BodyJson: TestDataBuilder.CreateTiptapJson("content"),
            CoverImageUrl: null,
            TagIds: null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _fixture.Mediator.Send(command));

        Assert.Contains(ex.Errors, e => e.PropertyName == "Title");
    }
}
```

#### Query Handler Test

```csharp
[Collection("Integration")]
public class GetPostBySlugTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public GetPostBySlugTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetPostBySlug_ExistingPublishedPost_ReturnsPostDetail()
    {
        // Arrange
        var post = TestDataBuilder.CreatePublishedPost(title: "My Test Post");
        _fixture.DbContext.Posts.Add(post);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _fixture.Mediator.Send(new GetPostBySlugQuery("my-test-post"));

        // Assert
        Assert.NotNull(result);
        Assert.Equal("My Test Post", result.Title);
        Assert.NotNull(result.BodyJson);
    }

    [Fact]
    public async Task GetPostBySlug_NonExistent_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<PostNotFoundException>(
            () => _fixture.Mediator.Send(new GetPostBySlugQuery("non-existent")));
    }
}
```

#### API Controller Test

```csharp
[Collection("Integration")]
public class PostsApiTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public PostsApiTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetPosts_ReturnsOkWithPaginatedList()
    {
        // Arrange — seed 5 published posts
        var posts = TestDataBuilder.CreatePublishedPosts(count: 5);
        _fixture.DbContext.Posts.AddRange(posts);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var response = await _fixture.HttpClient.GetAsync("/api/v1/posts?page=1&pageSize=10");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<PaginatedList<PostDto>>();
        Assert.NotNull(body);
        Assert.Equal(5, body.TotalCount);
        Assert.Equal(5, body.Items.Count);
    }

    [Fact]
    public async Task CreatePost_Unauthenticated_Returns401()
    {
        var response = await _fixture.HttpClient.PostAsJsonAsync("/api/v1/posts", new { Title = "Test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

### Test Data Builder

```csharp
// Helpers/TestDataBuilder.cs
public static class TestDataBuilder
{
    public static User CreateUser(string role = "Author", string displayName = "Test User")
    {
        return User.Create(Guid.NewGuid(), displayName, role);
    }

    public static Post CreatePublishedPost(string title = "Test Post", Guid? authorId = null)
    {
        var post = Post.Create(authorId ?? Guid.NewGuid(), title, Slug.Create(title));
        post.Publish();
        return post;
    }

    public static List<Post> CreatePublishedPosts(int count, Guid? authorId = null)
    {
        return Enumerable.Range(1, count)
            .Select(i => CreatePublishedPost($"Test Post {i}", authorId))
            .ToList();
    }

    public static object CreateTiptapJson(string text)
    {
        return new
        {
            type = "doc",
            content = new[]
            {
                new { type = "paragraph", content = new[] { new { type = "text", text } } }
            }
        };
    }
}
```

### Running Tests

```bash
# Run all integration tests
dotnet test apps/blog-api/tests/Blog.IntegrationTests

# Run specific test class
dotnet test --filter "FullyQualifiedName~CreatePostTests"

# Run with verbose output
dotnet test --verbosity detailed
```

### Key Rules

1. **Real databases** — Always use Testcontainers, never mock DbContext or Redis
2. **Reset between tests** — `TRUNCATE ... CASCADE` ensures test isolation
3. **Collection fixture** — Share containers across tests in a collection (expensive to start)
4. **Test both happy and error paths** — Validation errors, not found, unauthorized
5. **Verify persistence** — After commands, query the database to confirm writes
6. **Use projections in assertions** — Don't compare entire DTOs, check specific fields
