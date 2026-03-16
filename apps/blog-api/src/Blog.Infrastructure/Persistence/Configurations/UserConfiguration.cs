using Blog.Domain.Aggregates.Users;
using Blog.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blog.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        // Email VO conversion: stored as VARCHAR(256)
        builder.Property(u => u.Email)
            .HasMaxLength(256)
            .IsRequired()
            .HasConversion(e => e.Value, v => Email.Create(v));

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Bio).HasMaxLength(500);
        builder.Property(u => u.AvatarUrl).HasMaxLength(2048);
        builder.Property(u => u.Website).HasMaxLength(2048);

        // SocialLinks stored as JSONB (Dictionary<string,string>).
        // ValueComparer is required so EF Core can detect in-place dictionary modifications
        // when SaveChanges is called (e.g., adding/removing a social link key).
        // Without it, EF silently ignores mutations that don't replace the whole reference.
        var socialLinksComparer = new ValueComparer<Dictionary<string, string>>(
            (a, b) => System.Text.Json.JsonSerializer.Serialize(a, (System.Text.Json.JsonSerializerOptions?)null)
                   == System.Text.Json.JsonSerializer.Serialize(b, (System.Text.Json.JsonSerializerOptions?)null),
            v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null).GetHashCode(),
            v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                     System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                     (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, string>());

        builder.Property(u => u.SocialLinks)
            .HasColumnType("jsonb")
            .HasConversion(
                d => System.Text.Json.JsonSerializer.Serialize(d, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, string>(),
                socialLinksComparer);

        builder.Property(u => u.Role)
            .HasMaxLength(20)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(u => u.IsActive).HasDefaultValue(true);
    }
}
