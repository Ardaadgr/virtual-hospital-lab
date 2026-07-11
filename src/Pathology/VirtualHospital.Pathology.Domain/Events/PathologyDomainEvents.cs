using VirtualHospital.Pathology.Domain.Enums;
using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Pathology.Domain.Events;

/// <summary>
/// Domain events raised inside the pathology context. These stay INSIDE the
/// context. Anything that must reach HBYS, LIS or PACS is translated into an
/// integration event in VirtualHospital.Contracts at the boundary.
/// See .claude/rules/integration-messaging.md.
/// </summary>
public sealed record CaseAccessionedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid PathologyCaseId,
    string SpecimenCode,
    string MedicalRecordNumber) : IDomainEvent;

public sealed record CaseStageChangedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid PathologyCaseId,
    PathologyStage FromStage,
    PathologyStage ToStage,
    Guid PerformedByStaffId) : IDomainEvent;

public sealed record CasePathologistAssignedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid PathologyCaseId,
    Guid PathologistId) : IDomainEvent;

public sealed record BlockCreatedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid PathologyCaseId,
    string SpecimenCode,
    string BlockCode) : IDomainEvent;

public sealed record SlideCutDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid PathologyCaseId,
    string BlockCode,
    string SlideCode,
    bool IsAdditionalSection) : IDomainEvent;

public sealed record SlideScannedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid PathologyCaseId,
    string SlideCode,
    Guid DigitalSlideId,
    int ScanVersion) : IDomainEvent;

/// <summary>
/// Note that this event carries the id of the SUPERSEDED image, not a deletion
/// notice. The old image remains in the VNA. See ARCHITECTURE.md AD-010.
/// </summary>
public sealed record SlideRescannedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid PathologyCaseId,
    string SlideCode,
    Guid SupersededDigitalSlideId,
    Guid NewDigitalSlideId,
    RescanReason Reason) : IDomainEvent;

public sealed record ConsultationRequestedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid PathologyCaseId,
    Guid ConsultationId,
    Guid ConsultantPathologistId) : IDomainEvent;

public sealed record ConsultationAnsweredDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid PathologyCaseId,
    Guid ConsultationId,
    Guid ConsultantPathologistId) : IDomainEvent;

public sealed record CaseReportedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid PathologyCaseId,
    string MedicalRecordNumber,
    Guid EncounterId,
    Guid PathologistId) : IDomainEvent;
