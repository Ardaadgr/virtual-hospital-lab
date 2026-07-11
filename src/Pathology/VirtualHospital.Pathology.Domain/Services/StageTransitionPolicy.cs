using VirtualHospital.Pathology.Domain.Enums;
using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Pathology.Domain.Services;

/// <summary>
/// The legal stage transitions of a pathology case.
///
/// This matrix lives in the domain, not in the database and not in the UI.
/// The reason is concrete: a case that jumps from Accessioned straight to
/// Reported means a report was written for tissue that was never sectioned,
/// stained or looked at. That must be impossible to express, not merely
/// discouraged by a disabled button.
///
/// See ARCHITECTURE.md AD-012.
/// </summary>
public static class StageTransitionPolicy
{
    private static readonly IReadOnlyDictionary<PathologyStage, PathologyStage[]> Allowed =
        new Dictionary<PathologyStage, PathologyStage[]>
        {
            [PathologyStage.Accessioned] = new[] { PathologyStage.Grossing },
            [PathologyStage.Grossing] = new[] { PathologyStage.Processing },
            [PathologyStage.Processing] = new[] { PathologyStage.Embedding },
            [PathologyStage.Embedding] = new[] { PathologyStage.Sectioning },
            [PathologyStage.Sectioning] = new[] { PathologyStage.Staining },
            [PathologyStage.Staining] = new[] { PathologyStage.Scanning },
            [PathologyStage.Scanning] = new[] { PathologyStage.UnderReview },

            // The pathologist may send the case back for an additional section
            // (which re-enters the cutting/staining/scanning loop), refer it for
            // a second opinion, or report it.
            [PathologyStage.UnderReview] = new[]
            {
                PathologyStage.Sectioning,
                PathologyStage.InConsultation,
                PathologyStage.Reported,
            },

            // A consultation always returns to the ORIGINAL pathologist, who
            // retains authorship of the final report. The consultant cannot
            // move the case straight to Reported; see ARCHITECTURE.md AD-013.
            [PathologyStage.InConsultation] = new[] { PathologyStage.UnderReview },

            // Terminal. An amended report is a separate concept (a new report
            // version), not a backward stage transition.
            [PathologyStage.Reported] = Array.Empty<PathologyStage>(),
        };

    public static bool IsAllowed(PathologyStage from, PathologyStage to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyCollection<PathologyStage> AllowedFrom(PathologyStage from) =>
        Allowed.TryGetValue(from, out var targets)
            ? targets
            : Array.Empty<PathologyStage>();

    /// <summary>
    /// Throws if the transition is not legal. Called by PathologyCase before
    /// any stage change is recorded.
    /// </summary>
    public static void EnsureAllowed(PathologyStage from, PathologyStage to)
    {
        if (from == to)
        {
            throw new DomainException($"Case is already at stage {from}.");
        }

        if (!IsAllowed(from, to))
        {
            var permitted = AllowedFrom(from);
            var permittedText = permitted.Count == 0
                ? "none (terminal stage)"
                : string.Join(", ", permitted);

            throw new DomainException(
                $"Illegal stage transition {from} -> {to}. Permitted from {from}: {permittedText}.");
        }
    }
}
