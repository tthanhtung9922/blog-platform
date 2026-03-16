using Blog.Domain.Common;

namespace Blog.Domain.ValueObjects;

public sealed class ReadingTime : ValueObject
{
    private const int WordsPerMinute = 250;

    public int Minutes { get; }

    private ReadingTime(int minutes) => Minutes = minutes;

    public static ReadingTime FromWordCount(int wordCount)
    {
        if (wordCount < 0) throw new ArgumentOutOfRangeException(nameof(wordCount));
        var minutes = (int)Math.Ceiling((double)wordCount / WordsPerMinute);
        return new ReadingTime(Math.Max(1, minutes));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Minutes;
    }

    public override string ToString() => $"{Minutes} min read";
}
