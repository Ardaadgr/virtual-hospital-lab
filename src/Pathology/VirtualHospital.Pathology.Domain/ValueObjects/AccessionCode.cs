using System.Text.RegularExpressions;
using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Pathology.Domain.ValueObjects;

/// <summary>
/// Prefix of a pathology tracking code. The prefix identifies WHAT the code
/// labels, which is what makes a mis-scan detectable: scanning a block barcode
/// where a slide barcode is expected is rejected rather than silently accepted.
/// </summary>
public enum AccessionCodeKind
{
    /// <summary>Specimen (material) - prefix M.</summary>
    Specimen = 1,

    /// <summary>Paraffin block - prefix B.</summary>
    Block = 2,

    /// <summary>Glass slide - prefix S.</summary>
    Slide = 3,
}

/// <summary>
/// A pathology tracking code: a single letter prefix (M, B or S) followed by
/// digits. Issued automatically at accession by IAccessionCodeGenerator and
/// printed on the physical barcode label.
///
/// The three kinds form an unbroken chain: every B code belongs to an M code,
/// every S code belongs to a B code. Losing that link is the failure mode that
/// leads to a specimen being reported against the wrong patient, so the chain
/// is enforced in the domain and again by foreign keys in the database.
///
/// NOTE: the exact digit composition is not yet finalised. See
/// ARCHITECTURE.md AD-011 - the generator is configurable and this value
/// object only enforces the shape (prefix + digits), not a fixed length.
/// </summary>
public sealed partial class AccessionCode : ValueObject
{
    private const int MinDigits = 6;
    private const int MaxDigits = 20;

    private AccessionCode(AccessionCodeKind kind, string value)
    {
        Kind = kind;
        Value = value;
    }

    public AccessionCodeKind Kind { get; }

    public string Value { get; }

    public static char PrefixFor(AccessionCodeKind kind) => kind switch
    {
        AccessionCodeKind.Specimen => 'M',
        AccessionCodeKind.Block => 'B',
        AccessionCodeKind.Slide => 'S',
        _ => throw new DomainException($"Unknown accession code kind: {kind}."),
    };

    private static AccessionCodeKind KindFor(char prefix) => prefix switch
    {
        'M' => AccessionCodeKind.Specimen,
        'B' => AccessionCodeKind.Block,
        'S' => AccessionCodeKind.Slide,
        _ => throw new DomainException(
            $"Accession code prefix '{prefix}' is not recognised. Expected M, B or S."),
    };

    /// <summary>
    /// Parses a code read from a barcode scanner or persisted record.
    /// </summary>
    public static AccessionCode Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Accession code cannot be empty.");
        }

        var normalized = value.Trim().ToUpperInvariant();

        if (!CodePattern().IsMatch(normalized))
        {
            throw new DomainException(
                $"Accession code '{value}' is malformed. Expected M, B or S followed by " +
                $"{MinDigits} to {MaxDigits} digits.");
        }

        return new AccessionCode(KindFor(normalized[0]), normalized);
    }

    /// <summary>
    /// Parses a code and asserts it is of the expected kind. Use this at every
    /// barcode-scanning boundary: it is what turns "the technician scanned the
    /// block label instead of the slide label" from a silent data corruption
    /// into a rejected operation.
    /// </summary>
    public static AccessionCode ParseAs(string value, AccessionCodeKind expectedKind)
    {
        var code = Parse(value);

        if (code.Kind != expectedKind)
        {
            throw new DomainException(
                $"Expected a {expectedKind} code (prefix {PrefixFor(expectedKind)}) " +
                $"but '{code.Value}' is a {code.Kind} code.");
        }

        return code;
    }

    /// <summary>
    /// Builds a code from a prefix and an already-composed digit string.
    /// Only IAccessionCodeGenerator implementations should call this.
    /// </summary>
    public static AccessionCode FromDigits(AccessionCodeKind kind, string digits)
    {
        if (string.IsNullOrWhiteSpace(digits) || !digits.All(char.IsDigit))
        {
            throw new DomainException("Accession code body must contain digits only.");
        }

        return Parse($"{PrefixFor(kind)}{digits}");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[MBS]\d{6,20}$")]
    private static partial Regex CodePattern();
}
