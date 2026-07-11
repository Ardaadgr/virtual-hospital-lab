using System.Text.RegularExpressions;
using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Hbys.Domain.ValueObjects;

/// <summary>
/// The patient's hospital-wide identifier. It is issued once, on first
/// registration, and NEVER changes: if the patient returns five years later,
/// the same MRN is reused.
///
/// This is deliberately NOT the same thing as an Encounter (visit) id.
/// Clinical workflows attach to an Encounter; the MRN links a person's
/// entire history across all encounters. See ARCHITECTURE.md AD-003.
/// </summary>
public sealed partial class MedicalRecordNumber : ValueObject
{
    public const string Prefix = "MRN";

    private MedicalRecordNumber(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// Rehydrates an existing MRN, validating its shape. Use
    /// <see cref="IMedicalRecordNumberGenerator"/> to issue a new one.
    /// </summary>
    public static MedicalRecordNumber Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Medical record number cannot be empty.");
        }

        var normalized = value.Trim().ToUpperInvariant();

        if (!MrnPattern().IsMatch(normalized))
        {
            throw new DomainException(
                $"Medical record number '{value}' is malformed. Expected {Prefix} followed by 6 to 12 digits.");
        }

        return new MedicalRecordNumber(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^MRN\d{6,12}$")]
    private static partial Regex MrnPattern();
}
