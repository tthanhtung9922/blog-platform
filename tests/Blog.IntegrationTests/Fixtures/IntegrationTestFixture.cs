using Blog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;
using StackExchange.Redis;

namespace Blog.IntegrationTests.Fixtures;

public class IntegrationTestFixture : IAsyncLifetime
{
    public ApiFactory Factory { get; } = new ApiFactory();
    private Respawner _respawner = null!;
    private NpgsqlConnection _dbConnection = null!;

    public HttpClient HttpClient { get; private set; } = null!;
    public IServiceProvider Services => Factory.Services;

    public async Task InitializeAsync()
    {
        await Factory.InitializeAsync();
        HttpClient = Factory.CreateClient();

        // Apply migrations once at fixture startup
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        await db.Database.MigrateAsync();

        // Initialize Respawn for PostgreSQL — inspects schema to determine FK deletion order
        _dbConnection = new NpgsqlConnection(Factory.PostgresConnectionString);
        await _dbConnection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" },
            // CRITICAL: exclude EF migrations history — migrations run once at fixture init.
            // If Respawn deletes this table, MigrateAsync() on the next test sees empty history
            // and tries to re-run all migrations, failing with "table already exists".
            TablesToIgnore = new Table[] { new Table("public", "__EFMigrationsHistory") }
        });
    }

    /// <summary>
    /// Resets both database and Redis between tests.
    /// Database: Respawn deletes all rows in FK order (~20ms, no schema rebuild).
    /// Redis: FlushDatabaseAsync() clears all keys — prevents cache hits from previous test's data.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_dbConnection);

        // Also flush Redis — without this, a cached response from test N contaminates test N+1
        var muxer = Factory.Services.GetRequiredService<IConnectionMultiplexer>();
        await muxer.GetServer(muxer.GetEndPoints().First()).FlushDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbConnection.DisposeAsync();
        await Factory.DisposeAsync();
    }
}
