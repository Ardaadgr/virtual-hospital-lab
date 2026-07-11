namespace VirtualHospital.Pathology.Domain.Enums;

public enum DigitalSlideStatus
{
    /// <summary>The image the pathologist currently sees.</summary>
    Active = 1,

    /// <summary>
    /// Replaced by a newer scan. Hidden in the UI, but RETAINED in the VNA.
    /// A report may have been written against this image; deleting it would
    /// destroy the evidence trail. See ARCHITECTURE.md AD-010.
    /// </summary>
    Superseded = 2,
}
