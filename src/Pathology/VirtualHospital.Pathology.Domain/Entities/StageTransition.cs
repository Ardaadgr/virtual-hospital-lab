using VirtualHospital.Pathology.Domain.Enums;
using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Pathology.Domain.Entities;

/// <summary>
/// One step of the case through the laboratory. The chain of these records is
/// the ONLY source of truth for "where is this case now" - the current stage is
/// the last link, never a separate field kept in sync by hand.
///
/// This is also the audit trail: if a specimen goes missing, these rows say
/// which stage it last reached and who handled it.
/// </summary>
public sealed class StageTransition : Entity
{
    private StageTransition(
        Guid id,
        Guid pathologyCaseId,
        PathologyStage? fromStage,
        PathologyStage toStage,
        Guid performedByStaffId,
        DateTimeOffset occurredAtUtc,
        string? note)
        : base(id)
    {
        PathologyCaseId = pathologyCaseId;
        FromStage = fromStage;
        ToStage = toStage;
        PerformedByStaffId = performedByStaffId;
        OccurredAtUtc = occurredAtUtc;
        Note = note;
    }

    /// <summary>Required by EF Core.</summary>
    private StageTransition() { }

    public Guid PathologyCaseId { get; private set; }

    /// <summary>Null only for the very first transition (accession).</summary>
    public PathologyStage? FromStage { get; private set; }

    public PathologyStage ToStage { get; private set; }

    public Guid PerformedByStaffId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string? Note { get; private set; }

    internal static StageTransition Record(
        Guid pathologyCaseId,
        PathologyStage? fromStage,
        PathologyStage toStage,
        Guid performedByStaffId,
        IClock clock,
        string? note = null) =>
        new(Guid.NewGuid(), pathologyCaseId, fromStage, toStage, performedByStaffId, clock.UtcNow, note);
}
