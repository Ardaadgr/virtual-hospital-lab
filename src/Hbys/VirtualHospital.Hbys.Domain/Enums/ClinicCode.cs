namespace VirtualHospital.Hbys.Domain.Enums;

/// <summary>
/// Clinics supported by the hospital. The structure exists and encounters
/// attach to a clinic, but clinic-specific clinical logic (examination forms,
/// diagnosis entry, prescriptions) is out of scope for this phase.
/// See ARCHITECTURE.md AD-015.
/// </summary>
public enum ClinicCode
{
    InternalMedicine = 1,
    Surgery = 2,
    Radiology = 3,
    Laboratory = 4,
    Pathology = 5,
    Emergency = 6,
}
