using VirtualHospital.Hbys.Domain.Enums;
using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Hbys.Domain.Entities;

/// <summary>
/// A single visit. One Patient (one MRN) has many Encounters over their life.
/// Clinical orders (lab, pathology, radiology) attach to an Encounter, which
/// is what makes it possible to answer "which visit was this test ordered on".
/// </summary>
public sealed class Encounter : Entity
{
    private Encounter(
        Guid id,
        Guid patientId,
        ClinicCode clinic,
        EncounterType encounterType,
        Guid attendingStaffId,
        DateTimeOffset openedAtUtc)
        : base(id)
    {
        PatientId = patientId;
        Clinic = clinic;
        EncounterType = encounterType;
        AttendingStaffId = attendingStaffId;
        OpenedAtUtc = openedAtUtc;
    }

    /// <summary>Required by EF Core.</summary>
    private Encounter() { }

    public Guid PatientId { get; private set; }

    public ClinicCode Clinic { get; private set; }

    public EncounterType EncounterType { get; private set; }

    public Guid AttendingStaffId { get; private set; }

    public DateTimeOffset OpenedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public bool IsOpen => ClosedAtUtc is null;

    internal static Encounter Open(
        Guid patientId,
        ClinicCode clinic,
        EncounterType encounterType,
        Guid attendingStaffId,
        IClock clock) =>
        new(Guid.NewGuid(), patientId, clinic, encounterType, attendingStaffId, clock.UtcNow);

    public void Close(IClock clock)
    {
        if (!IsOpen)
        {
            throw new DomainException("Encounter is already closed.");
        }

        ClosedAtUtc = clock.UtcNow;
    }
}
