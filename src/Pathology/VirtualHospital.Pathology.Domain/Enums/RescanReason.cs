namespace VirtualHospital.Pathology.Domain.Enums;

/// <summary>Why a slide was scanned again, superseding the previous image.</summary>
public enum RescanReason
{
    BlurredImage = 1,
    FocusError = 2,
    DirtySlide = 3,
    IncompleteScan = 4,
    Other = 99,
}
