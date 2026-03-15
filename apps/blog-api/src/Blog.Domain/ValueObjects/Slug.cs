using System.Text;
using System.Text.RegularExpressions;
using Blog.Domain.Common;

namespace Blog.Domain.ValueObjects;

public sealed class Slug : ValueObject
{
    public string Value { get; }

    private Slug(string value) => Value = value;

    public static Slug Create(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        var slug = title.ToLowerInvariant();

        // Vietnamese-specific: replace đ before removing diacritics
        slug = slug.Replace("đ", "d").Replace("Đ", "d");

        // Remove diacritics (normalise to form D, then strip combining characters)
        var normalized = slug.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        slug = sb.ToString().Normalize(NormalizationForm.FormC);

        slug = Regex.Replace(slug, @"[^a-z0-9\s\-]", "");
        slug = Regex.Replace(slug, @"[\s\-]+", "-");
        slug = slug.Trim('-');

        if (string.IsNullOrEmpty(slug))
            throw new ArgumentException("Title produces an empty slug.", nameof(title));

        return new Slug(slug);
    }

    public static Slug FromExisting(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Slug value cannot be empty.", nameof(value));
        return new Slug(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
