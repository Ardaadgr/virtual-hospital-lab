using VirtualHospital.Pathology.Domain.ValueObjects;

namespace VirtualHospital.Pathology.Domain.Services;

/// <summary>
/// Issues the M / B / S tracking codes that are printed onto physical barcode
/// labels when a case is accessioned into pathology.
///
/// The codes are derived from the patient's case (exam) identifier. The exact
/// composition is NOT fixed by the domain: see ARCHITECTURE.md AD-011, which
/// records that the digit layout is still an open decision. That is precisely
/// why this is an interface - the layout can change in Infrastructure without
/// touching a single domain rule.
///
/// Uniqueness is the implementation's responsibility (a database sequence, not
/// an in-memory counter: two technicians accessioning at the same moment must
/// not receive the same code).
/// </summary>
public interface IAccessionCodeGenerator
{
    /// <summary>Issues the M code for a newly accessioned specimen.</summary>
    Task<AccessionCode> NextSpecimenCodeAsync(
        long caseSequence,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues the B code for a block cut from the given specimen. The specimen
    /// code is passed in so implementations can embed the parent's identity in
    /// the child's code, keeping the chain legible to a human reading a label.
    /// </summary>
    Task<AccessionCode> NextBlockCodeAsync(
        AccessionCode specimenCode,
        int blockSequence,
        CancellationToken cancellationToken = default);

    /// <summary>Issues the S code for a slide cut from the given block.</summary>
    Task<AccessionCode> NextSlideCodeAsync(
        AccessionCode blockCode,
        int slideSequence,
        CancellationToken cancellationToken = default);
}
