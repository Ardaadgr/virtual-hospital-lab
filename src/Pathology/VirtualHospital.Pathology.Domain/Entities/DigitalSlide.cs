using VirtualHospital.Pathology.Domain.Enums;
using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Pathology.Domain.Entities;

/// <summary>
/// A whole slide image (WSI) produced by scanning a glass slide. The pixels
/// live in the VNA as DICOM VL Whole Slide Microscopy; this entity holds only
/// the pointer and the provenance.
///
/// A slide may be scanned more than once (blurred image, focus error, dirty
/// slide). When that happens the NEW scan becomes active and the OLD one is
/// marked Superseded - it is NOT deleted. If a pathologist reported against
/// the earlier image, that image is the evidence for the report; destroying it
/// would leave the report unauditable. See ARCHITECTURE.md AD-010.
/// </summary>
public sealed class DigitalSlide : Entity
{
    private DigitalSlide(
        Guid id,
        Guid slideId,
        string dicomStudyInstanceUid,
        string dicomSeriesInstanceUid,
        string dicomSopInstanceUid,
        int scanVersion,
        Guid scannedByStaffId,
        DateTimeOffset scannedAtUtc,
        RescanReason? rescanReason)
        : base(id)
    {
        SlideId = slideId;
        DicomStudyInstanceUid = dicomStudyInstanceUid;
        DicomSeriesInstanceUid = dicomSeriesInstanceUid;
        DicomSopInstanceUid = dicomSopInstanceUid;
        ScanVersion = scanVersion;
        ScannedByStaffId = scannedByStaffId;
        ScannedAtUtc = scannedAtUtc;
        RescanReason = rescanReason;
        Status = DigitalSlideStatus.Active;
    }

    /// <summary>Required by EF Core.</summary>
    private DigitalSlide() { }

    public Guid SlideId { get; private set; }

    public string DicomStudyInstanceUid { get; private set; } = null!;

    public string DicomSeriesInstanceUid { get; private set; } = null!;

    public string DicomSopInstanceUid { get; private set; } = null!;

    /// <summary>1 for the first scan, 2 for the first rescan, and so on.</summary>
    public int ScanVersion { get; private set; }

    public DigitalSlideStatus Status { get; private set; }

    public Guid ScannedByStaffId { get; private set; }

    public DateTimeOffset ScannedAtUtc { get; private set; }

    /// <summary>Null for the first scan; mandatory for every rescan.</summary>
    public RescanReason? RescanReason { get; private set; }

    public DateTimeOffset? SupersededAtUtc { get; private set; }

    public bool IsActive => Status == DigitalSlideStatus.Active;

    internal static DigitalSlide Create(
        Guid slideId,
        string studyInstanceUid,
        string seriesInstanceUid,
        string sopInstanceUid,
        int scanVersion,
        Guid scannedByStaffId,
        RescanReason? rescanReason,
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUid)
            || string.IsNullOrWhiteSpace(seriesInstanceUid)
            || string.IsNullOrWhiteSpace(sopInstanceUid))
        {
            throw new DomainException(
                "A digital slide must carry the DICOM UIDs that locate it in the VNA.");
        }

        if (scanVersion < 1)
        {
            throw new DomainException("Scan version starts at 1.");
        }

        if (scanVersion > 1 && rescanReason is null)
        {
            throw new DomainException(
                "A rescan must record why the previous scan was inadequate.");
        }

        return new DigitalSlide(
            Guid.NewGuid(),
            slideId,
            studyInstanceUid,
            seriesInstanceUid,
            sopInstanceUid,
            scanVersion,
            scannedByStaffId,
            clock.UtcNow,
            rescanReason);
    }

    /// <summary>
    /// Marks this image as replaced by a newer scan. The image itself stays in
    /// the VNA; only its status changes. There is deliberately no Delete method
    /// on this class.
    /// </summary>
    internal void Supersede(IClock clock)
    {
        if (Status == DigitalSlideStatus.Superseded)
        {
            return;
        }

        Status = DigitalSlideStatus.Superseded;
        SupersededAtUtc = clock.UtcNow;
    }
}
