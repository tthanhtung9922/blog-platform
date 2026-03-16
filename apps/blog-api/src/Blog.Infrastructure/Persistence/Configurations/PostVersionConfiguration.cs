using Blog.Domain.Aggregates.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blog.Infrastructure.Persistence.Configurations;

public class PostVersionConfiguration : IEntityTypeConfiguration<PostVersion>
{
    public void Configure(EntityTypeBuilder<PostVersion> builder)
    {
        builder.ToTable("post_versions");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.BodyJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(v => v.VersionNumber).IsRequired();

        // Covering index for paginated version retrieval by post
        builder.HasIndex(v => new { v.PostId, v.CreatedAt });
    }
}
