using Blog.Domain.Aggregates.Posts;
using Blog.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blog.Infrastructure.Persistence.Configurations;

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("posts");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Title).HasMaxLength(256).IsRequired();

        // Value object conversion: Slug stored as VARCHAR(256)
        builder.Property(p => p.Slug)
            .HasMaxLength(256)
            .IsRequired()
            .HasConversion(s => s.Value, v => Slug.FromExisting(v));

        builder.HasIndex(p => p.Slug).IsUnique();

        // Enum stored as string for readability in DB
        builder.Property(p => p.Status)
            .HasMaxLength(20)
            .IsRequired()
            .HasConversion<string>();

        // Partial index: only index published posts (performance optimization)
        builder.HasIndex(p => p.Status)
            .HasFilter("status = 'Published'");

        builder.Property(p => p.Excerpt).HasMaxLength(500);
        builder.Property(p => p.CoverImageUrl).HasMaxLength(2048);
        builder.Property(p => p.IsFeatured).HasDefaultValue(false);

        // Value object conversion: ReadingTime stored as int (minutes)
        builder.Property(p => p.ReadingTime)
            .HasConversion(
                r => r == null ? (int?)null : r.Minutes,
                v => v == null ? null : ReadingTime.FromWordCount(v.Value * 250));

        // 1-to-1 relationship with PostContent (owned within the Post aggregate boundary)
        builder.HasOne(p => p.Content)
            .WithOne()
            .HasForeignKey<PostContent>(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1-to-many relationship with PostVersion (append-only snapshots)
        builder.HasMany(p => p.Versions)
            .WithOne()
            .HasForeignKey(v => v.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tag collection: map List<TagReference> VOs to a post_tags join table
        // EF Core maps owned entity collections — TagReference has only TagId
        builder.OwnsMany(p => p.Tags, tagBuilder =>
        {
            tagBuilder.ToTable("post_tags");
            tagBuilder.WithOwner().HasForeignKey("post_id");
            tagBuilder.Property(t => t.TagId).HasColumnName("tag_id").IsRequired();
            tagBuilder.HasKey("post_id", "tag_id");
        });
    }
}
