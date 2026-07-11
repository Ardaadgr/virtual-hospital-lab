namespace VirtualHospital.SharedKernel.Primitives;

/// <summary>
/// Abstracts the system clock so that time-dependent domain rules
/// (stage transitions, audit timestamps) can be tested deterministically.
/// Never call DateTimeOffset.UtcNow directly inside domain code.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
