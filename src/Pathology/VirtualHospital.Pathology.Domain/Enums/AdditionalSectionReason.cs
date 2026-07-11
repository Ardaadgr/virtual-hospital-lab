namespace VirtualHospital.Pathology.Domain.Enums;

/// <summary>
/// Why a second (or later) slide was cut from a block. Cutting an extra
/// section consumes tissue irreversibly, so the reason and the requesting
/// physician are both mandatory. See ARCHITECTURE.md AD-009.
/// </summary>
public enum AdditionalSectionReason
{
    AdditionalStainRequested = 1,
    InsufficientSection = 2,
    DeeperLevelRequested = 3,
    ConsultationRequest = 4,
    Other = 99,
}
