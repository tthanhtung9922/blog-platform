namespace Blog.Domain.Common;

public abstract class ValueObject
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is not ValueObject other) return false;
        if (GetType() != other.GetType()) return false;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
        => GetEqualityComponents().Aggregate(1, (current, obj) => HashCode.Combine(current, obj));

    public static bool operator ==(ValueObject? left, ValueObject? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(ValueObject? left, ValueObject? right)
        => !(left == right);
}
