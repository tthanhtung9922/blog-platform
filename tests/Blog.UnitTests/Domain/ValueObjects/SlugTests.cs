using Blog.Domain.ValueObjects;

namespace Blog.UnitTests.Domain.ValueObjects;

public class SlugTests
{
    [Fact]
    public void Create_WhenTitleIsHelloWorld_ReturnsHelloWorldSlug()
    {
        var slug = Slug.Create("Hello World");
        Assert.Equal("hello-world", slug.Value);
    }

    [Fact]
    public void Create_WhenTitleHasVietnameseDiacritics_ReturnsAsciiSlug()
    {
        var slug = Slug.Create("Xin chào Việt Nam");
        Assert.Equal("xin-chao-viet-nam", slug.Value);
    }

    [Fact]
    public void Create_WhenTitleIsEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Slug.Create(""));
    }

    [Fact]
    public void Equality_WhenSameValue_AreEqual()
    {
        var slug1 = Slug.Create("hello");
        var slug2 = Slug.Create("hello");
        Assert.Equal(slug1, slug2);
    }
}
