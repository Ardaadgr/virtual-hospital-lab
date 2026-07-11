using VirtualHospital.Pathology.Domain.Enums;
using VirtualHospital.Pathology.Domain.Events;
using VirtualHospital.Pathology.Domain.Services;
using VirtualHospital.Pathology.Domain.ValueObjects;
using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Pathology.Domain.Entities;

/// <summary>
/// Aggregate root of the pathology management system.
///
/// Everything that happens to a specimen happens through this class: the whole
/// specimen -> block -> slide -> image chain is reached through the case, never
/// out of the side. That is what keeps the barcode chain unbroken. A Block that
/// could be created without going through its Specimen is a Block whose parent
/// is a matter of opinion, and a specimen reported against the wrong patient is
/// the worst thing this system can do.
///
/// The case belongs to a patient (identified by MRN, a read-model copied from
/// HBYS via ADT messages) and to the encounter the request was raised on.
/// Pathology never reaches into the HBYS database. See ARCHITECTURE.md AD-002.
/// </summary>
public sealed class PathologyCase : AggregateRoot
{
    private readonly List<Specimen> _specimens = new();
    private readonly List<StageTransition> _stageTransitions = new();
    private readonly List<Consultation> _consultations = new();

    private PathologyCase(
        Guid id,
        string medicalRecordNumber,
        Guid encounterId,
        Guid orderingPhysicianId,
        string clinicalHistory)
        : base(id)
    {
        MedicalRecordNumber = medicalRecordNumber;
        EncounterId = encounterId;
        OrderingPhysicianId = orderingPhysicianId;
        ClinicalHistory = clinicalHistory;
    }

    /// <summary>Required by EF Core.</summary>
    private PathologyCase() { }

    /// <summary>
    /// The patient's lifetime hospital identifier, received from HBYS. Held as
    /// a string, not as the HBYS value object: pathology must not take a code
    /// dependency on another bounded context.
    /// </summary>
    public string MedicalRecordNumber { get; private set; } = null!;

    /// <summary>The visit this request was raised on. See ARCHITECTURE.md AD-003.</summary>
    public Guid EncounterId { get; private set; }

    public Guid OrderingPhysicianId { get; private set; }

    public string ClinicalHistory { get; private set; } = null!;

    /// <summary>The pathologist responsible for the case and its final report.</summary>
    public Guid? AssignedPathologistId { get; private set; }

    public string? Report { get; private set; }

    public DateTimeOffset? ReportedAtUtc { get; private set; }

    public IReadOnlyCollection<Specimen> Specimens => _specimens.AsReadOnly();

    public IReadOnlyCollection<StageTransition> StageTransitions => _stageTransitions.AsReadOnly();

    public IReadOnlyCollection<Consultation> Consultations => _consultations.AsReadOnly();

    /// <summary>
    /// The single source of truth for where the case is. Derived from the last
    /// transition rather than stored separately, so the two can never disagree.
    ///
    /// Deliberately reads the last APPENDED transition rather than sorting by
    /// timestamp. Two transitions recorded in the same clock tick (a fast
    /// automated step, or a fixed clock under test) would sort unstably, and an
    /// unstable sort here means reading the wrong stage. The list is append-only,
    /// so insertion order is the true order. Infrastructure must preserve it
    /// when rehydrating from the database.
    /// </summary>
    public PathologyStage CurrentStage =>
        _stageTransitions.Count == 0
            ? throw new DomainException("A pathology case always has at least the accession transition.")
            : _stageTransitions[^1].ToStage;

    /// <summary>
    /// Accessions a case: pathology takes receipt of the specimen and issues its
    /// M code. This is the only way a case comes into existence.
    /// </summary>
    public static PathologyCase Accession(
        string medicalRecordNumber,
        Guid encounterId,
        Guid orderingPhysicianId,
        string clinicalHistory,
        AccessionCode specimenCode,
        SpecimenType specimenType,
        string collectedFrom,
        DateTimeOffset collectedAtUtc,
        Guid accessionedByStaffId,
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(medicalRecordNumber))
        {
            throw new DomainException("A pathology case must be linked to a patient MRN.");
        }

        if (encounterId == Guid.Empty)
        {
            throw new DomainException("A pathology case must be linked to an encounter.");
        }

        if (string.IsNullOrWhiteSpace(clinicalHistory))
        {
            throw new DomainException(
                "Clinical history is required. A pathologist reading a slide without knowing why "
                + "it was taken is being asked to work blind.");
        }

        var pathologyCase = new PathologyCase(
            Guid.NewGuid(),
            medicalRecordNumber.Trim(),
            encounterId,
            orderingPhysicianId,
            clinicalHistory.Trim());

        var specimen = Specimen.Create(
            pathologyCase.Id,
            specimenCode,
            specimenType,
            collectedFrom,
            collectedAtUtc,
            accessionedByStaffId,
            clock);

        pathologyCase._specimens.Add(specimen);

        pathologyCase._stageTransitions.Add(StageTransition.Record(
            pathologyCase.Id,
            fromStage: null,
            toStage: PathologyStage.Accessioned,
            accessionedByStaffId,
            clock));

        pathologyCase.Raise(new CaseAccessionedDomainEvent(
            Guid.NewGuid(),
            clock.UtcNow,
            pathologyCase.Id,
            specimenCode.Value,
            pathologyCase.MedicalRecordNumber));

        return pathologyCase;
    }

    /// <summary>
    /// Moves the case to the next stage. Illegal transitions are rejected here,
    /// in the domain, so that no caller anywhere can express them.
    /// </summary>
    public void TransitionTo(
        PathologyStage toStage,
        Guid performedByStaffId,
        IClock clock,
        string? note = null)
    {
        var fromStage = CurrentStage;
        StageTransitionPolicy.EnsureAllowed(fromStage, toStage);

        _stageTransitions.Add(StageTransition.Record(
            Id, fromStage, toStage, performedByStaffId, clock, note));

        Raise(new CaseStageChangedDomainEvent(
            Guid.NewGuid(), clock.UtcNow, Id, fromStage, toStage, performedByStaffId));
    }

    public void AssignPathologist(Guid pathologistId, IClock clock)
    {
        if (pathologistId == Guid.Empty)
        {
            throw new DomainException("A valid pathologist is required.");
        }

        AssignedPathologistId = pathologistId;

        Raise(new CasePathologistAssignedDomainEvent(
            Guid.NewGuid(), clock.UtcNow, Id, pathologistId));
    }

    /// <summary>Records a block produced from a specimen during embedding.</summary>
    public Block AddBlock(
        Guid specimenId,
        AccessionCode blockCode,
        string? tissueDescription,
        Guid embeddedByStaffId,
        IClock clock)
    {
        var specimen = FindSpecimen(specimenId);
        var block = specimen.AddBlock(blockCode, tissueDescription, embeddedByStaffId, clock);

        Raise(new BlockCreatedDomainEvent(
            Guid.NewGuid(), clock.UtcNow, Id, specimen.Code.Value, block.Code.Value));

        return block;
    }

    /// <summary>Cuts the routine slide from a block. The normal 1:1 path.</summary>
    public Slide CutSlide(
        Guid specimenId,
        Guid blockId,
        AccessionCode slideCode,
        StainType stain,
        Guid cutByStaffId,
        IClock clock)
    {
        var block = FindSpecimen(specimenId).FindBlock(blockId);
        var slide = block.CutSlide(slideCode, stain, cutByStaffId, clock);

        Raise(new SlideCutDomainEvent(
            Guid.NewGuid(), clock.UtcNow, Id, block.Code.Value, slide.Code.Value, isAdditionalSection: false));

        return slide;
    }

    /// <summary>
    /// Cuts an additional section from a block, beyond the routine slide. The
    /// reason and the requesting physician are both mandatory.
    /// </summary>
    public Slide CutAdditionalSlide(
        Guid specimenId,
        Guid blockId,
        AccessionCode slideCode,
        StainType stain,
        AdditionalSectionReason reason,
        Guid requestedByPhysicianId,
        Guid cutByStaffId,
        IClock clock)
    {
        var block = FindSpecimen(specimenId).FindBlock(blockId);
        var slide = block.CutAdditionalSlide(
            slideCode, stain, reason, requestedByPhysicianId, cutByStaffId, clock);

        Raise(new SlideCutDomainEvent(
            Guid.NewGuid(), clock.UtcNow, Id, block.Code.Value, slide.Code.Value, isAdditionalSection: true));

        return slide;
    }

    /// <summary>Records the first scan of a slide.</summary>
    public DigitalSlide ScanSlide(
        Guid specimenId,
        Guid blockId,
        Guid slideId,
        string studyInstanceUid,
        string seriesInstanceUid,
        string sopInstanceUid,
        Guid scannedByStaffId,
        IClock clock)
    {
        var slide = FindSpecimen(specimenId).FindBlock(blockId).FindSlide(slideId);

        var digitalSlide = slide.AttachScan(
            studyInstanceUid, seriesInstanceUid, sopInstanceUid, scannedByStaffId, clock);

        Raise(new SlideScannedDomainEvent(
            Guid.NewGuid(), clock.UtcNow, Id, slide.Code.Value, digitalSlide.Id, digitalSlide.ScanVersion));

        return digitalSlide;
    }

    /// <summary>
    /// Rescans a slide. The new image becomes the active one; the previous image
    /// is superseded and KEPT. Nothing is deleted. See ARCHITECTURE.md AD-010.
    /// </summary>
    public DigitalSlide RescanSlide(
        Guid specimenId,
        Guid blockId,
        Guid slideId,
        string studyInstanceUid,
        string seriesInstanceUid,
        string sopInstanceUid,
        RescanReason reason,
        Guid scannedByStaffId,
        IClock clock)
    {
        var slide = FindSpecimen(specimenId).FindBlock(blockId).FindSlide(slideId);

        var supersededId = slide.CurrentDigitalSlideId;

        var digitalSlide = slide.Rescan(
            studyInstanceUid, seriesInstanceUid, sopInstanceUid, reason, scannedByStaffId, clock);

        Raise(new SlideRescannedDomainEvent(
            Guid.NewGuid(),
            clock.UtcNow,
            Id,
            slide.Code.Value,
            supersededDigitalSlideId: supersededId!.Value,
            newDigitalSlideId: digitalSlide.Id,
            reason));

        return digitalSlide;
    }

    /// <summary>
    /// Refers the case to a second pathologist. The case moves to InConsultation
    /// and the consultant gains view access, but the assigned pathologist remains
    /// the author of the final report.
    /// </summary>
    public Consultation RequestConsultation(
        Guid requestedByPathologistId,
        Guid consultantPathologistId,
        string question,
        IClock clock)
    {
        if (AssignedPathologistId is null)
        {
            throw new DomainException("The case has no assigned pathologist to request a consultation.");
        }

        if (requestedByPathologistId != AssignedPathologistId)
        {
            throw new DomainException(
                "Only the pathologist the case is assigned to may request a consultation on it.");
        }

        var consultation = Consultation.Request(
            Id, requestedByPathologistId, consultantPathologistId, question, clock);

        _consultations.Add(consultation);

        TransitionTo(PathologyStage.InConsultation, requestedByPathologistId, clock);

        Raise(new ConsultationRequestedDomainEvent(
            Guid.NewGuid(), clock.UtcNow, Id, consultation.Id, consultantPathologistId));

        return consultation;
    }

    /// <summary>
    /// The consultant records their opinion and the case returns to the assigned
    /// pathologist for reporting.
    /// </summary>
    public void RespondToConsultation(
        Guid consultationId,
        Guid respondingPathologistId,
        string opinion,
        IClock clock)
    {
        var consultation = _consultations.SingleOrDefault(c => c.Id == consultationId)
            ?? throw new DomainException($"Consultation {consultationId} does not belong to this case.");

        consultation.Respond(respondingPathologistId, opinion, clock);

        TransitionTo(PathologyStage.UnderReview, respondingPathologistId, clock);

        Raise(new ConsultationAnsweredDomainEvent(
            Guid.NewGuid(), clock.UtcNow, Id, consultation.Id, respondingPathologistId));
    }

    /// <summary>
    /// Finalises the report. Only the assigned pathologist may do this - not a
    /// consultant, and not a technician. A consultation is advice; the diagnosis
    /// and its consequences belong to the pathologist who signed for the case.
    /// </summary>
    public void CompleteReport(Guid pathologistId, string report, IClock clock)
    {
        if (AssignedPathologistId is null)
        {
            throw new DomainException("The case has no assigned pathologist.");
        }

        if (pathologistId != AssignedPathologistId)
        {
            throw new DomainException(
                "Only the assigned pathologist may write the final report. A consultant provides "
                + "an opinion; responsibility for the report is not transferred.");
        }

        if (string.IsNullOrWhiteSpace(report))
        {
            throw new DomainException("The report cannot be empty.");
        }

        EnsureEverySlideIsScanned();

        TransitionTo(PathologyStage.Reported, pathologistId, clock);

        Report = report.Trim();
        ReportedAtUtc = clock.UtcNow;

        Raise(new CaseReportedDomainEvent(
            Guid.NewGuid(),
            clock.UtcNow,
            Id,
            MedicalRecordNumber,
            EncounterId,
            pathologistId));
    }

    /// <summary>
    /// A report must be backed by scanned slides.
    ///
    /// Note the empty-collection check. "All slides are scanned" is vacuously
    /// true when there are no slides, which would let a case be reported with no
    /// tissue ever having been sectioned or looked at. The stage machine alone
    /// does not prevent this: a case can be walked through Grossing, Processing,
    /// Embedding and Sectioning without anyone actually creating a block or a
    /// slide record. So the check is made explicitly here.
    /// </summary>
    private void EnsureEverySlideIsScanned()
    {
        var slides = _specimens
            .SelectMany(s => s.Blocks)
            .SelectMany(b => b.Slides)
            .ToList();

        if (slides.Count == 0)
        {
            throw new DomainException(
                "This case has no slides. A report cannot be finalised for a case in which no "
                + "section was ever cut.");
        }

        var unscanned = slides.Where(sl => !sl.IsScanned).Select(sl => sl.Code.Value).ToList();

        if (unscanned.Count > 0)
        {
            throw new DomainException(
                "Every slide must be scanned before the report is finalised. Not yet scanned: "
                + string.Join(", ", unscanned) + ".");
        }
    }

    private Specimen FindSpecimen(Guid specimenId) =>
        _specimens.SingleOrDefault(s => s.Id == specimenId)
        ?? throw new DomainException($"Specimen {specimenId} does not belong to this case.");
}
