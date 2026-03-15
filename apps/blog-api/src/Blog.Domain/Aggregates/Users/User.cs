using Blog.Domain.Common;
using Blog.Domain.DomainEvents;
using Blog.Domain.Exceptions;
using Blog.Domain.ValueObjects;

namespace Blog.Domain.Aggregates.Users;

public class User : AggregateRoot<Guid>
{
    public Email Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Bio { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? Website { get; private set; }
    public Dictionary<string, string> SocialLinks { get; private set; } = new();
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private User() { }

    /// <summary>
    /// The Id parameter is the SAME Guid as the corresponding IdentityUser.Id (ADR-006).
    /// The User domain aggregate and IdentityUser share only this GUID — no inheritance,
    /// no navigation properties, no FK constraints.
    /// </summary>
    public static User Create(Guid id, Email email, string displayName, UserRole role = UserRole.Reader)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));

        return new User
        {
            Id = id,
            Email = email,
            DisplayName = displayName,
            Role = role,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateProfile(string displayName, string? bio, string? avatarUrl, string? website, Dictionary<string, string>? socialLinks)
    {
        DisplayName = displayName;
        Bio = bio;
        AvatarUrl = avatarUrl;
        Website = website;
        SocialLinks = socialLinks ?? new Dictionary<string, string>();
        AddDomainEvent(new UserProfileUpdatedEvent(Id));
    }

    public void AssignRole(UserRole role)
    {
        Role = role;
    }

    public void Ban()
    {
        if (!IsActive)
            throw new DomainException("User is already banned.");
        IsActive = false;
        AddDomainEvent(new UserBannedEvent(Id));
    }

    public void Reactivate()
    {
        IsActive = true;
    }
}
