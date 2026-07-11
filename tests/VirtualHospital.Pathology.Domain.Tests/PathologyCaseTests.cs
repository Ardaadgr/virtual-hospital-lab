using FluentAssertions;
using VirtualHospital.Pathology.Domain.Entities;
using VirtualHospital.Pathology.Domain.Enums;
using VirtualHospital.Pathology.Domain.ValueObjects;
using VirtualHospital.SharedKernel.Primitives;
using Xunit;

namespace VirtualHospital.Pathology.Domain.Tests;

/// <summary>
/// Fixed clock. Domain rules must never depend on wall-clock time, and tests
/// must never be flaky because of it.
/// </summary>
internal sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; } =
        new(2026, 7, 11, 9, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

/// <summary>
/// These tests encode the rules that make the pathology system safe. All test
/// data is synthetic; no real patient identifiers appear anywhere.
/// See .claude/rules/testing-strategy.md.
/// </summary>
public sealed class PathologyCaseTests
{
    private readonly FixedClock _clock = new();

    private static readonly Guid Technician = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Pathologist = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid Consultant = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid Physician = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

    private PathologyCase AccessionCase() =>
        PathologyCase.Accession(
            medicalRecordNumber: "MRN000123",
            encounterId: Guid.NewGuid(),
            orderingPhysicianId: Physician,
            clinicalHistory: "Sentetik test vakasi.",
            specimenCode: AccessionCode.Parse("M260000010"),
            specimenType: SpecimenType.Biopsy,
            collectedFrom: "Colon, sigmoid",
            collectedAtUtc: _clock.UtcNow.AddHours(-2),
            accessionedByStaffId: Technician,
            clock: _clock);

    // ---------- Stage machine ----------

    [Fact]
    public void Accession_Always_StartsAtAccessionedStage()
    {
        var sut = AccessionCase();

        sut.CurrentStage.Should().Be(PathologyStage.Accessioned);
        sut.StageTransitions.Should().ContainSingle();
    }

    [Fact]
    public void TransitionTo_AccessionedDirectlyToReported_ThrowsDomainException()
    {
        var sut = AccessionCase();

        var act = () => sut.TransitionTo(PathologyStage.Reported, Technician, _clock);

        act.Should().Throw<DomainException>()
            .WithMessage("*Illegal stage transition*");
    }

    [Fact]
    public void TransitionTo_SkippingStaining_ThrowsDomainException()
    {
        var sut = AccessionCase();
        sut.TransitionTo(PathologyStage.Grossing, Technician, _clock);
        sut.TransitionTo(PathologyStage.Processing, Technician, _clock);
        sut.TransitionTo(PathologyStage.Embedding, Technician, _clock);
        sut.TransitionTo(PathologyStage.Sectioning, Technician, _clock);

        // Sectioning -> Scanning skips Staining. An unstained section is not
        // diagnostic; scanning it would produce a useless image.
        var act = () => sut.TransitionTo(PathologyStage.Scanning, Technician, _clock);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CurrentStage_TransitionsInSameClockTick_ReadsLastAppended()
    {
        // The clock does not advance between these calls. If CurrentStage sorted
        // by timestamp it would be ambiguous here; it must read insertion order.
        var sut = AccessionCase();
        sut.TransitionTo(PathologyStage.Grossing, Technician, _clock);
        sut.TransitionTo(PathologyStage.Processing, Technician, _clock);

        sut.CurrentStage.Should().Be(PathologyStage.Processing);
    }

    // ---------- Block to slide: default 1:1 ----------

    [Fact]
    public void CutSlide_CalledTwiceOnSameBlock_ThrowsDomainException()
    {
        var (sut, specimenId, blockId) = CaseWithBlock();

        sut.CutSlide(specimenId, blockId, AccessionCode.Parse("S260000010101"),
            StainType.HematoxylinEosin, Technician, _clock);

        var act = () => sut.CutSlide(specimenId, blockId, AccessionCode.Parse("S260000010102"),
            StainType.HematoxylinEosin, Technician, _clock);

        act.Should().Throw<DomainException>()
            .WithMessage("*additional section*");
    }

    [Fact]
    public void CutAdditionalSlide_WithReasonAndRequestingPhysician_Succeeds()
    {
        var (sut, specimenId, blockId) = CaseWithBlock();
        sut.CutSlide(specimenId, blockId, AccessionCode.Parse("S260000010101"),
            StainType.HematoxylinEosin, Technician, _clock);

        var extra = sut.CutAdditionalSlide(
            specimenId, blockId, AccessionCode.Parse("S260000010102"),
            StainType.Immunohistochemistry,
            AdditionalSectionReason.AdditionalStainRequested,
            requestedByPhysicianId: Pathologist,
            cutByStaffId: Technician,
            clock: _clock);

        extra.IsAdditionalSection.Should().BeTrue();
        extra.SequenceInBlock.Should().Be(2);
        extra.RequestedByPhysicianId.Should().Be(Pathologist);
        extra.AdditionalSectionReason.Should().Be(AdditionalSectionReason.AdditionalStainRequested);
    }

    [Fact]
    public void CutAdditionalSlide_WithoutRequestingPhysician_ThrowsDomainException()
    {
        var (sut, specimenId, blockId) = CaseWithBlock();
        sut.CutSlide(specimenId, blockId, AccessionCode.Parse("S260000010101"),
            StainType.HematoxylinEosin, Technician, _clock);

        var act = () => sut.CutAdditionalSlide(
            specimenId, blockId, AccessionCode.Parse("S260000010102"),
            StainType.Immunohistochemistry,
            AdditionalSectionReason.AdditionalStainRequested,
            requestedByPhysicianId: Guid.Empty,
            cutByStaffId: Technician,
            clock: _clock);

        act.Should().Throw<DomainException>()
            .WithMessage("*requesting physician*");
    }

    [Fact]
    public void CutAdditionalSlide_BeforeRoutineSlide_ThrowsDomainException()
    {
        var (sut, specimenId, blockId) = CaseWithBlock();

        var act = () => sut.CutAdditionalSlide(
            specimenId, blockId, AccessionCode.Parse("S260000010101"),
            StainType.Immunohistochemistry,
            AdditionalSectionReason.DeeperLevelRequested,
            Pathologist, Technician, _clock);

        act.Should().Throw<DomainException>();
    }

    // ---------- Rescan: supersede, never delete ----------

    [Fact]
    public void RescanSlide_Always_KeepsPreviousImageAsSuperseded()
    {
        var (sut, specimenId, blockId, slideId) = CaseWithScannedSlide();

        var slideBefore = SlideOf(sut);
        var firstScanId = slideBefore.CurrentDigitalSlideId!.Value;

        _clock.Advance(TimeSpan.FromMinutes(30));

        var rescan = sut.RescanSlide(
            specimenId, blockId, slideId,
            "1.2.826.0.1.3680043.8.498.1", "1.2.826.0.1.3680043.8.498.2",
            "1.2.826.0.1.3680043.8.498.4",
            RescanReason.BlurredImage, Technician, _clock);

        var slide = SlideOf(sut);

        // The new scan is what the pathologist sees.
        slide.CurrentDigitalSlideId.Should().Be(rescan.Id);
        rescan.ScanVersion.Should().Be(2);
        rescan.RescanReason.Should().Be(RescanReason.BlurredImage);

        // The old scan is STILL THERE. This is the whole point: a report may
        // have been written against it, and deleting it would destroy the
        // evidence for that report. See ARCHITECTURE.md AD-010.
        slide.DigitalSlides.Should().HaveCount(2);
        var superseded = slide.DigitalSlides.Single(ds => ds.Id == firstScanId);
        superseded.Status.Should().Be(DigitalSlideStatus.Superseded);
        superseded.SupersededAtUtc.Should().NotBeNull();

        // Exactly one image is active at any moment.
        slide.DigitalSlides.Count(ds => ds.IsActive).Should().Be(1);
    }

    [Fact]
    public void ScanSlide_OnAlreadyScannedSlide_ThrowsDomainException()
    {
        var (sut, specimenId, blockId, slideId) = CaseWithScannedSlide();

        var act = () => sut.ScanSlide(specimenId, blockId, slideId,
            "1.2.3", "1.2.4", "1.2.5", Technician, _clock);

        act.Should().Throw<DomainException>()
            .WithMessage("*Rescan*");
    }

    // ---------- Barcode chain ----------

    [Fact]
    public void Specimen_LabelledWithBlockCode_ThrowsDomainException()
    {
        // Scanning a B label where an M label was expected must be rejected,
        // not silently accepted. A broken chain is how a specimen ends up
        // reported against the wrong patient.
        var act = () => PathologyCase.Accession(
            "MRN000123", Guid.NewGuid(), Physician, "Sentetik.",
            AccessionCode.Parse("B260000010"),
            SpecimenType.Biopsy, "Colon", _clock.UtcNow.AddHours(-1), Technician, _clock);

        act.Should().Throw<DomainException>().WithMessage("*M code*");
    }

    [Fact]
    public void ParseAs_WrongKind_ThrowsDomainException()
    {
        var act = () => AccessionCode.ParseAs("B260000010", AccessionCodeKind.Slide);

        act.Should().Throw<DomainException>();
    }

    // ---------- Consultation ----------

    [Fact]
    public void RequestConsultation_Always_MovesCaseToInConsultation()
    {
        var sut = CaseUnderReview();
        sut.AssignPathologist(Pathologist, _clock);

        sut.RequestConsultation(Pathologist, Consultant, "Ayirici tani gorusu.", _clock);

        sut.CurrentStage.Should().Be(PathologyStage.InConsultation);
        sut.Consultations.Should().ContainSingle();
    }

    [Fact]
    public void CompleteReport_ByConsultant_ThrowsDomainException()
    {
        var sut = CaseUnderReview();
        sut.AssignPathologist(Pathologist, _clock);
        var consultation = sut.RequestConsultation(Pathologist, Consultant, "Gorus.", _clock);
        sut.RespondToConsultation(consultation.Id, Consultant, "Benign gorunuyor.", _clock);

        // A consultation is advice. Responsibility for the report does not move.
        var act = () => sut.CompleteReport(Consultant, "Rapor.", _clock);

        act.Should().Throw<DomainException>()
            .WithMessage("*assigned pathologist*");
    }

    [Fact]
    public void CompleteReport_ByAssignedPathologistAfterConsultation_Succeeds()
    {
        var sut = CaseUnderReview();
        sut.AssignPathologist(Pathologist, _clock);
        var consultation = sut.RequestConsultation(Pathologist, Consultant, "Gorus.", _clock);
        sut.RespondToConsultation(consultation.Id, Consultant, "Benign gorunuyor.", _clock);

        sut.CompleteReport(Pathologist, "Benign. Malignite bulgusu yok.", _clock);

        sut.CurrentStage.Should().Be(PathologyStage.Reported);
        sut.ReportedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void RespondToConsultation_ByWrongPathologist_ThrowsDomainException()
    {
        var sut = CaseUnderReview();
        sut.AssignPathologist(Pathologist, _clock);
        var consultation = sut.RequestConsultation(Pathologist, Consultant, "Gorus.", _clock);

        var someoneElse = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
        var act = () => sut.RespondToConsultation(consultation.Id, someoneElse, "Gorusum.", _clock);

        act.Should().Throw<DomainException>();
    }

    // ---------- Report guards ----------

    [Fact]
    public void CompleteReport_WithNoSlidesAtAll_ThrowsDomainException()
    {
        // Walking the stage machine without ever creating a slide must not
        // produce a reportable case. "All slides scanned" is vacuously true on
        // an empty set, so this is checked explicitly.
        var sut = AccessionCase();
        sut.TransitionTo(PathologyStage.Grossing, Technician, _clock);
        sut.TransitionTo(PathologyStage.Processing, Technician, _clock);
        sut.TransitionTo(PathologyStage.Embedding, Technician, _clock);
        sut.TransitionTo(PathologyStage.Sectioning, Technician, _clock);
        sut.TransitionTo(PathologyStage.Staining, Technician, _clock);
        sut.TransitionTo(PathologyStage.Scanning, Technician, _clock);
        sut.TransitionTo(PathologyStage.UnderReview, Technician, _clock);
        sut.AssignPathologist(Pathologist, _clock);

        var act = () => sut.CompleteReport(Pathologist, "Rapor.", _clock);

        act.Should().Throw<DomainException>().WithMessage("*no slides*");
    }

    [Fact]
    public void CompleteReport_WithUnscannedSlide_ThrowsDomainException()
    {
        var (sut, specimenId, blockId) = CaseWithBlock();
        sut.CutSlide(specimenId, blockId, AccessionCode.Parse("S260000010101"),
            StainType.HematoxylinEosin, Technician, _clock);
        // Slide is never scanned.
        sut.TransitionTo(PathologyStage.Staining, Technician, _clock);
        sut.TransitionTo(PathologyStage.Scanning, Technician, _clock);
        sut.TransitionTo(PathologyStage.UnderReview, Technician, _clock);
        sut.AssignPathologist(Pathologist, _clock);

        var act = () => sut.CompleteReport(Pathologist, "Rapor.", _clock);

        act.Should().Throw<DomainException>().WithMessage("*must be scanned*");
    }

    // ---------- Helpers ----------

    private (PathologyCase, Guid SpecimenId, Guid BlockId) CaseWithBlock()
    {
        var sut = AccessionCase();
        sut.TransitionTo(PathologyStage.Grossing, Technician, _clock);
        sut.TransitionTo(PathologyStage.Processing, Technician, _clock);
        sut.TransitionTo(PathologyStage.Embedding, Technician, _clock);

        var specimen = sut.Specimens.Single();
        var block = sut.AddBlock(specimen.Id, AccessionCode.Parse("B26000001001"),
            "Sigmoid kolon, sentetik.", Technician, _clock);

        sut.TransitionTo(PathologyStage.Sectioning, Technician, _clock);

        return (sut, specimen.Id, block.Id);
    }

    private (PathologyCase, Guid SpecimenId, Guid BlockId, Guid SlideId) CaseWithScannedSlide()
    {
        var (sut, specimenId, blockId) = CaseWithBlock();

        var slide = sut.CutSlide(specimenId, blockId, AccessionCode.Parse("S260000010101"),
            StainType.HematoxylinEosin, Technician, _clock);

        sut.TransitionTo(PathologyStage.Staining, Technician, _clock);
        sut.TransitionTo(PathologyStage.Scanning, Technician, _clock);

        sut.ScanSlide(specimenId, blockId, slide.Id,
            "1.2.826.0.1.3680043.8.498.1", "1.2.826.0.1.3680043.8.498.2",
            "1.2.826.0.1.3680043.8.498.3", Technician, _clock);

        return (sut, specimenId, blockId, slide.Id);
    }

    private PathologyCase CaseUnderReview()
    {
        var (sut, _, _, _) = CaseWithScannedSlide();
        sut.TransitionTo(PathologyStage.UnderReview, Technician, _clock);
        return sut;
    }

    private static Slide SlideOf(PathologyCase c) =>
        c.Specimens.Single().Blocks.Single().Slides.Single();
}
