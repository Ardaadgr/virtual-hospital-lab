namespace VirtualHospital.Pathology.Domain.Enums;

/// <summary>
/// Stain applied to a slide. This lives at SLIDE level, not block level:
/// several slides cut from the same block may carry different stains.
/// </summary>
public enum StainType
{
    HematoxylinEosin = 1,
    Immunohistochemistry = 2,
    PeriodicAcidSchiff = 3,
    MassonTrichrome = 4,
    Other = 99,
}
