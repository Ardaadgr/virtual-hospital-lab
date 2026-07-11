namespace VirtualHospital.SharedKernel.Primitives;

/// <summary>
/// Raised when a domain invariant would be violated. This is a business rule
/// failure, not a technical fault, and maps to HTTP 422 at the API boundary.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
