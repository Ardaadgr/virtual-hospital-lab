using VirtualHospital.Hbys.Domain.Enums;
using VirtualHospital.Hbys.Domain.Events;
using VirtualHospital.Hbys.Domain.ValueObjects;
using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Hbys.Domain.Entities;

/// <summary>
/// Aggregate root for a person known to the hospital.
///
/// A Patient owns exactly one <see cref="MedicalRecordNumber"/> for life, and
/// many <see cref="Encounter"/> records (one per visit). Clinical orders never
/// attach to the Patient directly; they attach to an Encounter.
/// See ARCHITECTURE.md AD-003.
/// </summary>
public sealed class Patient : AggregateRoot
{
    private readonly List<Encounter> _encounters = new();

    private Patient(
        Guid id,
        MedicalRecordNumber medicalRecordNumber,
        NationalIdentifier nationalIdentifier,
        string givenName,
        string familyName,
        DateOnly dateOfBirth,
        Sex sex)
        : base(id)
    {
        MedicalRecordNumber = medicalRecordNumber;
        NationalIdentifier = nationalIdentifier;
        GivenName = givenName;
        FamilyName = familyName;
        DateOfBirth = dateOfBirth;
        Sex = sex;
    }

    /// <summary>Required by EF Core.</summary>
    private Patient() { }

    /// <summary>Issued once, on first registration. Never reassigned.</summary>
    public MedicalRecordNumber MedicalRecordNumber { get; private set; } = null!;

    public NationalIdentifier NationalIdentifier { get; private set; } = null!;

    public string GivenName { get; private set; } = null!;

    public string FamilyName { get; private set; } = null!;

    public DateOnly DateOfBirth { get; private set; }

    public Sex Sex { get; private set; }

    public IReadOnlyCollection<Encounter> Encounters => _encounters.AsReadOnly();

    /// <summary>
    /// Registers a person for the first time. The caller is responsible for
    /// having already searched for an existing record: creating a second
    /// Patient for someone who already has an MRN splits their clinical
    /// history and is a patient-safety defect, not merely a data-quality one.
    /// </summary>
    public static Patient Register(
        MedicalRecordNumber medicalRecordNumber,
        NationalIdentifier nationalIdentifier,
        string givenName,
        string familyName,
        DateOnly dateOfBirth,
        Sex sex,
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(givenName))
        {
            throw new DomainException("Given name is required.");
        }

        if (string.IsNullOrWhiteSpace(familyName))
        {
            throw new DomainException("Family name is required.");
        }

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        if (dateOfBirth > today)
        {
            throw new DomainException("Date of birth cannot be in the future.");
        }

        var patient = new Patient(
            Guid.NewGuid(),
            medicalRecordNumber,
            nationalIdentifier,
            givenName.Trim(),
            familyName.Trim(),
            dateOfBirth,
            sex);

        patient.Raise(new PatientRegisteredDomainEvent(
            Guid.NewGuid(),
            clock.UtcNow,
            patient.Id,
            medicalRecordNumber.Value));

        return patient;
    }

    /// <summary>
    /// Opens a new visit. A returning patient keeps the same MRN and simply
    /// gains another Encounter.
    /// </summary>
    public Encounter OpenEncounter(
        ClinicCode clinic,
        EncounterType encounterType,
        Guid attendingStaffId,
        IClock clock)
    {
        var encounter = Encounter.Open(Id, clinic, encounterType, attendingStaffId, clock);
        _encounters.Add(encounter);

        Raise(new EncounterOpenedDomainEvent(
            Guid.NewGuid(),
            clock.UtcNow,
            Id,
            MedicalRecordNumber.Value,
            encounter.Id,
            clinic));

        return encounter;
    }

    /// <summary>
    /// Updates demographics. The MRN is deliberately not updatable: it is the
    /// stable anchor for the patient's entire history.
    /// </summary>
    public void UpdateDemographics(string givenName, string familyName, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(givenName) || string.IsNullOrWhiteSpace(familyName))
        {
            throw new DomainException("Given name and family name are required.");
        }

        GivenName = givenName.Trim();
        FamilyName = familyName.Trim();

        Raise(new PatientDemographicsUpdatedDomainEvent(
            Guid.NewGuid(),
            clock.UtcNow,
            Id,
            MedicalRecordNumber.Value));
    }
}
