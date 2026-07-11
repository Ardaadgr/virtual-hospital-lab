using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Hbys.Domain.ValueObjects;

/// <summary>
/// Turkish national identity number (TC Kimlik No), used for patient identity
/// matching so that a returning patient is not issued a second MRN.
///
/// PRIVACY: this is sensitive personal data. It must never appear in logs,
/// URLs or error messages. See .claude/rules/data-privacy-kvkk.md.
/// </summary>
public sealed class NationalIdentifier : ValueObject
{
    private NationalIdentifier(string value) => Value = value;

    public string Value { get; }

    public static NationalIdentifier Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("National identifier cannot be empty.");
        }

        var normalized = value.Trim();

        if (normalized.Length != 11 || !normalized.All(char.IsDigit))
        {
            throw new DomainException("National identifier must be exactly 11 digits.");
        }

        if (!HasValidChecksum(normalized))
        {
            throw new DomainException("National identifier checksum is invalid.");
        }

        return new NationalIdentifier(normalized);
    }

    /// <summary>
    /// Validates the two check digits of a Turkish national identity number.
    /// This catches typos at the point of entry, which is exactly where a
    /// duplicate-patient defect would otherwise be introduced.
    /// </summary>
    private static bool HasValidChecksum(string value)
    {
        var digits = value.Select(c => c - '0').ToArray();

        if (digits[0] == 0)
        {
            return false;
        }

        var oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        var evenSum = digits[1] + digits[3] + digits[5] + digits[7];

        var tenth = ((oddSum * 7) - evenSum) % 10;
        if (tenth < 0)
        {
            tenth += 10;
        }

        if (tenth != digits[9])
        {
            return false;
        }

        var eleventh = digits.Take(10).Sum() % 10;
        return eleventh == digits[10];
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>
    /// Deliberately masked. Prevents the raw identifier leaking into logs or
    /// exception messages through an accidental string interpolation.
    /// </summary>
    public override string ToString() => $"*******{Value[^4..]}";
}
