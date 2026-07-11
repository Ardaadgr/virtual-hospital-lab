namespace VirtualHospital.Contracts.Pathology;

/// <summary>
/// The pathology report is final (HL7 ORU^R01). HBYS consumes this and attaches
/// the report to the patient record.
/// </summary>
public sealed record PathologyReportCompletedIntegrationEvent(
    Guid CorrelationId,
    Guid PathologyCaseId,
    string MedicalRecordNumber,
    Guid EncounterId,
    Guid PathologistId,
    string Report,
    DateTimeOffset ReportedAtUtc);

/// <summary>
/// A slide has been digitised and is available in the VNA. Carries the DICOM
/// UIDs so the viewer can retrieve it over DICOMweb.
/// </summary>
public sealed record PathologySlideScannedIntegrationEvent(
    Guid CorrelationId,
    Guid PathologyCaseId,
    string SlideCode,
    string DicomStudyInstanceUid,
    string DicomSeriesInstanceUid,
    string DicomSopInstanceUid,
    int ScanVersion,
    DateTimeOffset ScannedAtUtc);

/// <summary>
/// A slide was rescanned. The previous image is SUPERSEDED, not deleted, and
/// remains retrievable from the VNA for audit. See ARCHITECTURE.md AD-010.
/// Consumers must repoint to the new image; they must not delete the old one.
/// </summary>
public sealed record PathologySlideRescannedIntegrationEvent(
    Guid CorrelationId,
    Guid PathologyCaseId,
    string SlideCode,
    Guid SupersededDigitalSlideId,
    Guid NewDigitalSlideId,
    string RescanReason,
    DateTimeOffset ScannedAtUtc);

/// <summary>Progress notification so HBYS can show where a case has got to.</summary>
public sealed record PathologyCaseStageChangedIntegrationEvent(
    Guid CorrelationId,
    Guid PathologyCaseId,
    string MedicalRecordNumber,
    string FromStage,
    string ToStage,
    DateTimeOffset OccurredOnUtc);
