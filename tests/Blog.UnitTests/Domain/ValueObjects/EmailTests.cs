using Blog.Domain.ValueObjects;

namespace Blog.UnitTests.Domain.ValueObjects;

public class EmailTests
{
    [Fact]
    public void Create_WhenValidEmail_Succeeds()
    {
        var email = Email.Create("user@example.com");
        Assert.Equal("user@example.com", email.Value);
    }

    [Fact]
    public void Create_WhenInvalidEmail_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Email.Create("not-an-email"));
    }
}
