using VirtualHospital.Hbys.Domain.ValueObjects;

namespace VirtualHospital.Hbys.Domain.Services;

/// <summary>
/// Issues a new, unique MRN. Implemented in Infrastructure (backed by a
/// database sequence so that concurrent registrations cannot collide).
///
/// This is only called when the patient has NO existing record. Re-registering
/// a known patient must reuse the existing MRN.
/// </summary>
public interface IMedicalRecordNumberGenerator
{
    Task<MedicalRecordNumber> NextAsync(CancellationToken cancellationToken = default);
}
