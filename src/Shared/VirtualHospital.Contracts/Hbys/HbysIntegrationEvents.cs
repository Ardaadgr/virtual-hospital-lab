namespace VirtualHospital.Contracts.Hbys;

/// <summary>
/// Published when a patient is registered or admitted (HL7 ADT^A04).
/// LIS, PACS and Pathology consume this to build their local patient
/// read-models. They never query the HBYS database directly.
/// </summary>
public sealed record PatientRegisteredIntegrationEvent(
    Guid CorrelationId,
    Guid PatientId,
    string MedicalRecordNumber,
    string GivenName,
    string FamilyName,
    DateOnly DateOfBirth,
    string Sex,
    DateTimeOffset OccurredOnUtc);

/// <summary>Published when patient demographics change (HL7 ADT^A08).</summary>
public sealed record PatientDemographicsUpdatedIntegrationEvent(
    Guid CorrelationId,
    Guid PatientId,
    string MedicalRecordNumber,
    string GivenName,
    string FamilyName,
    DateTimeOffset OccurredOnUtc);

/// <summary>
/// A physician orders a pathology examination (HL7 ORM^O01).
/// Consumed by the pathology context, which accessions a case in response.
/// </summary>
public sealed record PathologyExaminationOrderedIntegrationEvent(
    Guid CorrelationId,
    Guid EncounterId,
    string MedicalRecordNumber,
    Guid OrderingPhysicianId,
    string ClinicalHistory,
    string SpecimenType,
    string CollectedFrom,
    DateTimeOffset CollectedAtUtc,
    DateTimeOffset OccurredOnUtc);

/// <summary>A physician orders a laboratory test (HL7 ORM^O01).</summary>
public sealed record LabTestOrderedIntegrationEvent(
    Guid CorrelationId,
    Guid EncounterId,
    string MedicalRecordNumber,
    Guid OrderingPhysicianId,
    IReadOnlyCollection<string> TestCodes,
    DateTimeOffset OccurredOnUtc);
