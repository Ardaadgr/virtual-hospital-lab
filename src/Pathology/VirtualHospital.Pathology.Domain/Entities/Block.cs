using VirtualHospital.Pathology.Domain.Enums;
using VirtualHospital.Pathology.Domain.ValueObjects;
using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Pathology.Domain.Entities;

/// <summary>
/// A paraffin block: a piece of tissue sampled from a specimen during grossing
/// and embedded in wax, labelled with a B code.
///
/// A block normally yields exactly ONE slide. Further slides are the exception,
/// not the rule, and require an explicit additional-section request carrying a
/// reason and the requesting physician. See ARCHITECTURE.md AD-009.
/// </summary>
public sealed class Block : Entity
{
    private readonly List<Slide> _slides = new();

    private Block(
        Guid id,
        Guid specimenId,
        AccessionCode code,
        int sequenceInSpecimen,
        string? tissueDescription,
        Guid embeddedByStaffId,
        DateTimeOffset embeddedAtUtc)
        : base(id)
    {
        SpecimenId = specimenId;
        Code = code;
        SequenceInSpecimen = sequenceInSpecimen;
        TissueDescription = tissueDescription;
        EmbeddedByStaffId = embeddedByStaffId;
        EmbeddedAtUtc = embeddedAtUtc;
    }

    /// <summary>Required by EF Core.</summary>
    private Block() { }

    public Guid SpecimenId { get; private set; }

    /// <summary>The B code printed on the physical label.</summary>
    public AccessionCode Code { get; private set; } = null!;

    public int SequenceInSpecimen { get; private set; }

    /// <summary>What tissue this block holds, as recorded at grossing.</summary>
    public string? TissueDescription { get; private set; }

    public Guid EmbeddedByStaffId { get; private set; }

    public DateTimeOffset EmbeddedAtUtc { get; private set; }

    public IReadOnlyCollection<Slide> Slides => _slides.AsReadOnly();

    public bool HasRoutineSlide => _slides.Count > 0;

    internal static Block Create(
        Guid specimenId,
        AccessionCode code,
        int sequenceInSpecimen,
        string? tissueDescription,
        Guid embeddedByStaffId,
        IClock clock)
    {
        if (code.Kind != AccessionCodeKind.Block)
        {
            throw new DomainException(
                $"A block must be labelled with a B code, but '{code.Value}' is a {code.Kind} code.");
        }

        if (sequenceInSpecimen < 1)
        {
            throw new DomainException("Block sequence within a specimen starts at 1.");
        }

        return new Block(
            Guid.NewGuid(),
            specimenId,
            code,
            sequenceInSpecimen,
            tissueDescription,
            embeddedByStaffId,
            clock.UtcNow);
    }

    /// <summary>
    /// Cuts the routine slide from this block. This is the normal 1:1 path and
    /// may be done only once; a second routine slide is not a thing that exists.
    /// </summary>
    internal Slide CutSlide(
        AccessionCode slideCode,
        StainType stain,
        Guid cutByStaffId,
        IClock clock)
    {
        if (HasRoutineSlide)
        {
            throw new DomainException(
                $"Block {Code.Value} has already produced its routine slide. A further slide is "
                + "an additional section and must be requested explicitly, with a reason and a "
                + "requesting physician.");
        }

        var slide = Slide.Create(
            Id,
            slideCode,
            stain,
            sequenceInBlock: 1,
            additionalSectionReason: null,
            requestedByPhysicianId: null,
            cutByStaffId,
            clock);

        _slides.Add(slide);
        return slide;
    }

    /// <summary>
    /// Cuts an ADDITIONAL section from this block, beyond the routine slide.
    ///
    /// This is deliberately a separate method rather than an optional argument
    /// on CutSlide. Cutting extra sections consumes tissue that cannot be got
    /// back, and a year later someone may need to answer "who asked for this
    /// and why". Making it a distinct, justified operation is the point.
    /// </summary>
    internal Slide CutAdditionalSlide(
        AccessionCode slideCode,
        StainType stain,
        AdditionalSectionReason reason,
        Guid requestedByPhysicianId,
        Guid cutByStaffId,
        IClock clock)
    {
        if (!HasRoutineSlide)
        {
            throw new DomainException(
                $"Block {Code.Value} has no routine slide yet. Cut the routine slide first; "
                + "an additional section is by definition additional to it.");
        }

        if (requestedByPhysicianId == Guid.Empty)
        {
            throw new DomainException(
                "An additional section requires the identity of the requesting physician.");
        }

        var slide = Slide.Create(
            Id,
            slideCode,
            stain,
            sequenceInBlock: _slides.Count + 1,
            additionalSectionReason: reason,
            requestedByPhysicianId: requestedByPhysicianId,
            cutByStaffId,
            clock);

        _slides.Add(slide);
        return slide;
    }

    internal Slide FindSlide(Guid slideId) =>
        _slides.SingleOrDefault(s => s.Id == slideId)
        ?? throw new DomainException($"Slide {slideId} does not belong to block {Code.Value}.");
}
