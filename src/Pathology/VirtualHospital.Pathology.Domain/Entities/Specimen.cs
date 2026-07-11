using VirtualHospital.Pathology.Domain.Enums;
using VirtualHospital.Pathology.Domain.ValueObjects;
using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Pathology.Domain.Entities;

/// <summary>
/// The tissue received from the patient, labelled with an M code at accession.
/// Grossing samples it into one or more blocks.
/// </summary>
public sealed class Specimen : Entity
{
    private readonly List<Block> _blocks = new();

    private Specimen(
        Guid id,
        Guid pathologyCaseId,
        AccessionCode code,
        SpecimenType specimenType,
        string collectedFrom,
        DateTimeOffset collectedAtUtc,
        Guid accessionedByStaffId,
        DateTimeOffset accessionedAtUtc)
        : base(id)
    {
        PathologyCaseId = pathologyCaseId;
        Code = code;
        SpecimenType = specimenType;
        CollectedFrom = collectedFrom;
        CollectedAtUtc = collectedAtUtc;
        AccessionedByStaffId = accessionedByStaffId;
        AccessionedAtUtc = accessionedAtUtc;
    }

    /// <summary>Required by EF Core.</summary>
    private Specimen() { }

    public Guid PathologyCaseId { get; private set; }

    /// <summary>The M code printed on the physical label.</summary>
    public AccessionCode Code { get; private set; } = null!;

    public SpecimenType SpecimenType { get; private set; }

    /// <summary>Anatomical site the tissue was taken from.</summary>
    public string CollectedFrom { get; private set; } = null!;

    public DateTimeOffset CollectedAtUtc { get; private set; }

    public Guid AccessionedByStaffId { get; private set; }

    public DateTimeOffset AccessionedAtUtc { get; private set; }

    public IReadOnlyCollection<Block> Blocks => _blocks.AsReadOnly();

    internal static Specimen Create(
        Guid pathologyCaseId,
        AccessionCode code,
        SpecimenType specimenType,
        string collectedFrom,
        DateTimeOffset collectedAtUtc,
        Guid accessionedByStaffId,
        IClock clock)
    {
        if (code.Kind != AccessionCodeKind.Specimen)
        {
            throw new DomainException(
                $"A specimen must be labelled with an M code, but '{code.Value}' is a {code.Kind} code.");
        }

        if (string.IsNullOrWhiteSpace(collectedFrom))
        {
            throw new DomainException(
                "The anatomical site is required. A specimen whose origin is unrecorded cannot "
                + "be safely reported.");
        }

        if (collectedAtUtc > clock.UtcNow)
        {
            throw new DomainException("Collection time cannot be in the future.");
        }

        return new Specimen(
            Guid.NewGuid(),
            pathologyCaseId,
            code,
            specimenType,
            collectedFrom.Trim(),
            collectedAtUtc,
            accessionedByStaffId,
            clock.UtcNow);
    }

    internal Block AddBlock(
        AccessionCode blockCode,
        string? tissueDescription,
        Guid embeddedByStaffId,
        IClock clock)
    {
        var block = Block.Create(
            Id,
            blockCode,
            sequenceInSpecimen: _blocks.Count + 1,
            tissueDescription,
            embeddedByStaffId,
            clock);

        _blocks.Add(block);
        return block;
    }

    internal Block FindBlock(Guid blockId) =>
        _blocks.SingleOrDefault(b => b.Id == blockId)
        ?? throw new DomainException($"Block {blockId} does not belong to specimen {Code.Value}.");
}
