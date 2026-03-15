using Blog.Domain.Aggregates.Tags;
using Blog.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blog.Infrastructure.Persistence.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();

        builder.Property(t => t.Slug)
            .HasMaxLength(100)
            .IsRequired()
            .HasConversion(s => s.Value, v => Slug.FromExisting(v));

        builder.HasIndex(t => t.Slug).IsUnique();
        builder.HasIndex(t => t.Name).IsUnique();
    }
}
