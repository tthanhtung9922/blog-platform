using Blog.Domain.ValueObjects;

namespace Blog.UnitTests.Domain.ValueObjects;

public class ReadingTimeTests
{
    [Fact]
    public void FromWordCount_When250Words_Returns1Minute()
    {
        var rt = ReadingTime.FromWordCount(250);
        Assert.Equal(1, rt.Minutes);
    }

    [Fact]
    public void FromWordCount_When500Words_Returns2Minutes()
    {
        var rt = ReadingTime.FromWordCount(500);
        Assert.Equal(2, rt.Minutes);
    }
}
