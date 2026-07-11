using VirtualHospital.Hbys.Domain.Enums;
using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Hbys.Domain.Events;

public sealed record PatientRegisteredDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid PatientId,
    string MedicalRecordNumber) : IDomainEvent;

public sealed record PatientDemographicsUpdatedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid PatientId,
    string MedicalRecordNumber) : IDomainEvent;

public sealed record EncounterOpenedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid PatientId,
    string MedicalRecordNumber,
    Guid EncounterId,
    ClinicCode Clinic) : IDomainEvent;
