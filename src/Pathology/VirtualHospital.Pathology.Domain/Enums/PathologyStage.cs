namespace VirtualHospital.Pathology.Domain.Enums;

/// <summary>
/// Stages of the physical laboratory workflow, in order. The case's current
/// stage is read from the last StageTransition, never stored in two places.
/// Transition legality is enforced by StageTransitionPolicy.
/// See ARCHITECTURE.md AD-012.
/// </summary>
public enum PathologyStage
{
    /// <summary>Specimen received by pathology; M code issued.</summary>
    Accessioned = 1,

    /// <summary>Macroscopic examination and sampling.</summary>
    Grossing = 2,

    /// <summary>Tissue processing (dehydration, clearing, paraffin infiltration).</summary>
    Processing = 3,

    /// <summary>Paraffin blocks created; B codes issued.</summary>
    Embedding = 4,

    /// <summary>Microtome sectioning; slides created, S codes issued.</summary>
    Sectioning = 5,

    /// <summary>Staining (H and E, IHC, special stains).</summary>
    Staining = 6,

    /// <summary>Digital scanning; whole slide image produced and archived.</summary>
    Scanning = 7,

    /// <summary>Pathologist is reviewing the case.</summary>
    UnderReview = 8,

    /// <summary>Referred to a second pathologist for an opinion.</summary>
    InConsultation = 9,

    /// <summary>Report finalised and sent to HBYS.</summary>
    Reported = 10,
}
