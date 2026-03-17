using Blog.API.Middleware;
using Blog.Application;
using Blog.Infrastructure;
using Blog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// MVC + OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Blog Application layer (MediatR pipeline: Validation → Logging → Authorization → Caching)
builder.Services.AddBlogApplication();

// Blog Infrastructure layer (repos, UoW, Redis, CurrentUserService, NoOp stubs)
builder.Services.AddBlogInfrastructure(builder.Configuration);

// BlogDbContext: kept in Blog.API for EF Core migration tooling compatibility.
// `dotnet ef migrations` requires the DbContext registration to be reachable from the
// startup project. AddBlogInfrastructure() handles all other infrastructure concerns.
builder.Services.AddDbContext<BlogDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention());

// JWT Bearer authentication — validates tokens issued by the configured issuer.
// ICurrentUserService reads claims from IHttpContextAccessor after this middleware runs.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]
                    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured."))),
        };
    });
builder.Services.AddAuthorization();

// GlobalExceptionHandler: maps Application exceptions to RFC 9457 ProblemDetails responses.
// ValidationException → 422, NotFoundException → 404, ForbiddenAccessException → 403
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Health check: verifies PostgreSQL is reachable
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BlogDbContext>("database");

var app = builder.Build();

// Run EF Core migrations on startup (dev pattern — Phase 9 replaces with migration script)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();

// Required for WebApplicationFactory<Program> in Blog.IntegrationTests.
// Top-level statement Programs produce an internal Program class — this declaration
// makes it public/accessible across assembly boundaries for test bootstrapping.
public partial class Program { }
