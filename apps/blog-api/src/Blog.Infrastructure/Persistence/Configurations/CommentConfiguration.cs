using Blog.Domain.Aggregates.Comments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blog.Infrastructure.Persistence.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Body).HasMaxLength(5000).IsRequired();

        builder.Property(c => c.Status)
            .HasMaxLength(20)
            .IsRequired()
            .HasConversion<string>();

        // ParentId is nullable — null means top-level comment
        builder.Property(c => c.ParentId).IsRequired(false);

        // Self-referencing FK (for reply lookup) — no cascade delete to avoid cycles
        builder.HasIndex(c => new { c.PostId, c.CreatedAt });
    }
}
