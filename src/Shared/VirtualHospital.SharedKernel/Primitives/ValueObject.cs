namespace VirtualHospital.SharedKernel.Primitives;

/// <summary>
/// Value objects have no identity. Two value objects with the same
/// components are the same value.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>Components that define this value's identity.</summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other) =>
        other is not null
        && other.GetType() == GetType()
        && other.GetEqualityComponents().SequenceEqual(GetEqualityComponents());

    public override bool Equals(object? obj) => obj is ValueObject vo && Equals(vo);

    public override int GetHashCode() =>
        GetEqualityComponents()
            .Aggregate(0, (hash, component) => HashCode.Combine(hash, component));
}
