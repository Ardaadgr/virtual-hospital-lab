namespace VirtualHospital.SharedKernel.Primitives;

/// <summary>
/// Base class for entities identified by a <see cref="Guid"/>.
/// Equality is identity-based, not value-based.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    protected Entity(Guid id) => Id = id;

    /// <summary>Required by EF Core materialization.</summary>
    protected Entity() { }

    public Guid Id { get; private init; }

    public bool Equals(Entity? other) =>
        other is not null && other.GetType() == GetType() && other.Id == Id;

    public override bool Equals(object? obj) => obj is Entity entity && Equals(entity);

    public override int GetHashCode() => Id.GetHashCode();
}
