using Blog.Domain.Aggregates.Comments;
using Blog.Domain.Aggregates.Posts;
using Blog.Domain.Aggregates.Tags;
using Blog.Domain.Aggregates.Users;
using Microsoft.EntityFrameworkCore;

namespace Blog.Infrastructure.Persistence;

public class BlogDbContext : DbContext
{
    public BlogDbContext(DbContextOptions<BlogDbContext> options) : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostContent> PostContents => Set<PostContent>();
    public DbSet<PostVersion> PostVersions => Set<PostVersion>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all IEntityTypeConfiguration<T> classes in this assembly automatically
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BlogDbContext).Assembly);
    }
}
