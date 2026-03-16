using Blog.Domain.ValueObjects;

namespace Blog.UnitTests.Domain.ValueObjects;

public class TagReferenceTests
{
    [Fact]
    public void Equality_WhenSameTagId_AreEqual()
    {
        var tagId = Guid.NewGuid();
        var ref1 = TagReference.Create(tagId);
        var ref2 = TagReference.Create(tagId);
        Assert.Equal(ref1, ref2);
    }
}
