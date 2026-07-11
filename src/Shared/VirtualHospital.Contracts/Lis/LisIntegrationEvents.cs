namespace VirtualHospital.Contracts.Lis;

/// <summary>A single laboratory result line (HL7 OBX).</summary>
public sealed record LabResultLine(
    string TestCode,
    string TestName,
    string Value,
    string? Unit,
    string? ReferenceRange,
    string AbnormalFlag);

/// <summary>
/// Laboratory results are ready (HL7 ORU^R01).
///
/// AutoVerified indicates the result passed the autoverification rules and was
/// released without a technician looking at it. Critical (panic) values are
/// NEVER autoverified; they always pass through human review. See the
/// lis-domain skill.
/// </summary>
public sealed record LabResultAvailableIntegrationEvent(
    Guid CorrelationId,
    Guid LabOrderId,
    string MedicalRecordNumber,
    Guid EncounterId,
    IReadOnlyCollection<LabResultLine> Results,
    bool AutoVerified,
    DateTimeOffset ResultedAtUtc);
