using Blog.Domain.Aggregates.Users;
using Blog.Domain.DomainEvents;
using Blog.Domain.Exceptions;
using Blog.Domain.ValueObjects;

namespace Blog.UnitTests.Domain.Aggregates;

public class UserTests
{
    [Fact]
    public void Create_WhenValidArgs_IsActiveAndRoleSet()
    {
        var user = User.Create(Guid.NewGuid(), Email.Create("test@example.com"), "Test User", UserRole.Author);
        Assert.True(user.IsActive);
        Assert.Equal(UserRole.Author, user.Role);
    }

    [Fact]
    public void Ban_WhenActive_IsActiveIsFalse()
    {
        var user = User.Create(Guid.NewGuid(), Email.Create("test@example.com"), "Test User");
        user.Ban();

        Assert.False(user.IsActive);
        Assert.Contains(user.DomainEvents, e => e is UserBannedEvent);
    }
}
