namespace VirtualHospital.Contracts.Pacs;

/// <summary>A radiology study has been archived to the VNA and is viewable.</summary>
public sealed record RadiologyStudyArchivedIntegrationEvent(
    Guid CorrelationId,
    string MedicalRecordNumber,
    Guid EncounterId,
    string Modality,
    string DicomStudyInstanceUid,
    int InstanceCount,
    DateTimeOffset ArchivedAtUtc);

/// <summary>The radiologist's report is final (HL7 ORU^R01).</summary>
public sealed record RadiologyReportCompletedIntegrationEvent(
    Guid CorrelationId,
    string MedicalRecordNumber,
    Guid EncounterId,
    string DicomStudyInstanceUid,
    Guid RadiologistId,
    string Report,
    DateTimeOffset ReportedAtUtc);
