using VirtualHospital.Pathology.Domain.Enums;
using VirtualHospital.Pathology.Domain.ValueObjects;
using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Pathology.Domain.Entities;

/// <summary>
/// A glass slide: a stained section cut from a paraffin block, labelled with
/// an S code.
///
/// A slide holds MANY DigitalSlide records over its life (one per scan) but
/// exactly ONE of them is active at a time. <see cref="CurrentDigitalSlideId"/>
/// is the single answer to "which image does the pathologist see". Rescanning
/// repoints it and supersedes the previous image; it never deletes anything.
/// See ARCHITECTURE.md AD-010.
/// </summary>
public sealed class Slide : Entity
{
    private readonly List<DigitalSlide> _digitalSlides = new();

    private Slide(
        Guid id,
        Guid blockId,
        AccessionCode code,
        StainType stain,
        int sequenceInBlock,
        AdditionalSectionReason? additionalSectionReason,
        Guid? requestedByPhysicianId,
        Guid cutByStaffId,
        DateTimeOffset cutAtUtc)
        : base(id)
    {
        BlockId = blockId;
        Code = code;
        Stain = stain;
        SequenceInBlock = sequenceInBlock;
        AdditionalSectionReason = additionalSectionReason;
        RequestedByPhysicianId = requestedByPhysicianId;
        CutByStaffId = cutByStaffId;
        CutAtUtc = cutAtUtc;
    }

    /// <summary>Required by EF Core.</summary>
    private Slide() { }

    public Guid BlockId { get; private set; }

    /// <summary>The S code printed on the physical label.</summary>
    public AccessionCode Code { get; private set; } = null!;

    /// <summary>
    /// Stain lives here, not on the Block: several slides cut from one block
    /// may carry different stains (H and E, IHC, PAS).
    /// </summary>
    public StainType Stain { get; private set; }

    /// <summary>1 for the routine slide; 2 and above are additional sections.</summary>
    public int SequenceInBlock { get; private set; }

    /// <summary>Null for the routine first slide; mandatory for additional sections.</summary>
    public AdditionalSectionReason? AdditionalSectionReason { get; private set; }

    /// <summary>Who asked for the additional section. Null for the routine slide.</summary>
    public Guid? RequestedByPhysicianId { get; private set; }

    public Guid CutByStaffId { get; private set; }

    public DateTimeOffset CutAtUtc { get; private set; }

    /// <summary>
    /// The image currently shown to the pathologist. Null until first scanned.
    /// </summary>
    public Guid? CurrentDigitalSlideId { get; private set; }

    /// <summary>
    /// Every scan ever taken of this slide, active and superseded. Superseded
    /// entries are retained for audit; they are simply not shown in the viewer.
    /// </summary>
    public IReadOnlyCollection<DigitalSlide> DigitalSlides => _digitalSlides.AsReadOnly();

    public bool IsScanned => CurrentDigitalSlideId is not null;

    /// <summary>True when this slide exists because someone asked for an extra section.</summary>
    public bool IsAdditionalSection => SequenceInBlock > 1;

    internal static Slide Create(
        Guid blockId,
        AccessionCode code,
        StainType stain,
        int sequenceInBlock,
        AdditionalSectionReason? additionalSectionReason,
        Guid? requestedByPhysicianId,
        Guid cutByStaffId,
        IClock clock)
    {
        if (code.Kind != AccessionCodeKind.Slide)
        {
            throw new DomainException(
                $"A slide must be labelled with an S code, but '{code.Value}' is a {code.Kind} code.");
        }

        if (sequenceInBlock < 1)
        {
            throw new DomainException("Slide sequence within a block starts at 1.");
        }

        // The routine slide needs no justification. Every slide after it does:
        // cutting another section consumes tissue that cannot be recovered.
        if (sequenceInBlock > 1)
        {
            if (additionalSectionReason is null)
            {
                throw new DomainException(
                    "An additional section requires a reason. Only the first slide from a "
                    + "block is routine; cutting further sections consumes tissue irreversibly.");
            }

            if (requestedByPhysicianId is null || requestedByPhysicianId == Guid.Empty)
            {
                throw new DomainException(
                    "An additional section requires the identity of the requesting physician.");
            }
        }
        else
        {
            if (additionalSectionReason is not null || requestedByPhysicianId is not null)
            {
                throw new DomainException(
                    "The first slide from a block is routine and carries no additional-section request.");
            }
        }

        return new Slide(
            Guid.NewGuid(),
            blockId,
            code,
            stain,
            sequenceInBlock,
            additionalSectionReason,
            requestedByPhysicianId,
            cutByStaffId,
            clock.UtcNow);
    }

    /// <summary>
    /// Records the first scan of this slide.
    /// </summary>
    internal DigitalSlide AttachScan(
        string studyInstanceUid,
        string seriesInstanceUid,
        string sopInstanceUid,
        Guid scannedByStaffId,
        IClock clock)
    {
        if (IsScanned)
        {
            throw new DomainException(
                $"Slide {Code.Value} has already been scanned. Use Rescan to replace the image, "
                + "which supersedes the previous scan rather than discarding it.");
        }

        var digitalSlide = DigitalSlide.Create(
            Id,
            studyInstanceUid,
            seriesInstanceUid,
            sopInstanceUid,
            scanVersion: 1,
            scannedByStaffId,
            rescanReason: null,
            clock);

        _digitalSlides.Add(digitalSlide);
        CurrentDigitalSlideId = digitalSlide.Id;

        return digitalSlide;
    }

    /// <summary>
    /// Scans the slide again. The new image becomes the one the pathologist
    /// sees; the previous image is marked Superseded and RETAINED.
    ///
    /// This is a logical overwrite, not a physical one. The pathologist's view
    /// behaves as though the old image is gone, but a later audit can still
    /// establish which image a given report was written against.
    /// </summary>
    internal DigitalSlide Rescan(
        string studyInstanceUid,
        string seriesInstanceUid,
        string sopInstanceUid,
        RescanReason reason,
        Guid scannedByStaffId,
        IClock clock)
    {
        if (!IsScanned)
        {
            throw new DomainException(
                $"Slide {Code.Value} has never been scanned; there is nothing to rescan.");
        }

        var previous = _digitalSlides.Single(ds => ds.Id == CurrentDigitalSlideId);
        previous.Supersede(clock);

        var digitalSlide = DigitalSlide.Create(
            Id,
            studyInstanceUid,
            seriesInstanceUid,
            sopInstanceUid,
            scanVersion: previous.ScanVersion + 1,
            scannedByStaffId,
            reason,
            clock);

        _digitalSlides.Add(digitalSlide);
        CurrentDigitalSlideId = digitalSlide.Id;

        return digitalSlide;
    }
}
