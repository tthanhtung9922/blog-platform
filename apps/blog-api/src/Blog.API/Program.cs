using Blog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register BlogDbContext with Npgsql provider + snake_case naming convention
builder.Services.AddDbContext<BlogDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention());  // EFCore.NamingConventions 10.0.1

// Health check — verifies PostgreSQL is reachable
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BlogDbContext>("database");

var app = builder.Build();

// Run EF Core migrations on startup (Phase 1 / local dev pattern)
// Phase 2+ will add resilience/retry for production
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    await db.Database.MigrateAsync();
}

app.MapHealthChecks("/healthz");

app.Run();
