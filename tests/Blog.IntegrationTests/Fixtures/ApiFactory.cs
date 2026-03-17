using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Blog.IntegrationTests.Fixtures;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("blog_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:8")
        .Build();

    public string PostgresConnectionString => _postgres.GetConnectionString();
    public string RedisConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            // Override connection strings to point at Testcontainers instances
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["ConnectionStrings:Redis"] = _redis.GetConnectionString(),
            });
        });
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        await base.DisposeAsync();
    }
}
