using Blog.Domain.Common;

namespace Blog.Domain.ValueObjects;

public sealed class TagReference : ValueObject
{
    public Guid TagId { get; }

    private TagReference(Guid tagId) => TagId = tagId;

    public static TagReference Create(Guid tagId)
    {
        if (tagId == Guid.Empty)
            throw new ArgumentException("TagId cannot be empty.", nameof(tagId));
        return new TagReference(tagId);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return TagId;
    }
}
