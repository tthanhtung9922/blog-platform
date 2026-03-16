using Blog.Domain.Aggregates.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blog.Infrastructure.Persistence.Configurations;

public class PostContentConfiguration : IEntityTypeConfiguration<PostContent>
{
    public void Configure(EntityTypeBuilder<PostContent> builder)
    {
        builder.ToTable("post_contents");
        builder.HasKey(c => c.Id);

        // ProseMirror JSON stored as JSONB column for efficient querying
        builder.Property(c => c.BodyJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(c => c.BodyHtml)
            .HasColumnType("text")
            .IsRequired();
    }
}
