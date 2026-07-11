using VirtualHospital.SharedKernel.Primitives;

namespace VirtualHospital.Pathology.Domain.Entities;

/// <summary>
/// A second opinion. The reporting pathologist refers the case to a colleague,
/// who gains view access to the case and its slides (ABAC) and writes an
/// opinion.
///
/// The consultant does NOT take over the case. Authorship of the final report
/// stays with the original pathologist; a consultation is advice, not a
/// transfer of responsibility. See ARCHITECTURE.md AD-013.
/// </summary>
public sealed class Consultation : Entity
{
    private Consultation(
        Guid id,
        Guid pathologyCaseId,
        Guid requestedByPathologistId,
        Guid consultantPathologistId,
        string question,
        DateTimeOffset requestedAtUtc)
        : base(id)
    {
        PathologyCaseId = pathologyCaseId;
        RequestedByPathologistId = requestedByPathologistId;
        ConsultantPathologistId = consultantPathologistId;
        Question = question;
        RequestedAtUtc = requestedAtUtc;
    }

    /// <summary>Required by EF Core.</summary>
    private Consultation() { }

    public Guid PathologyCaseId { get; private set; }

    /// <summary>The pathologist who owns the case and will write the report.</summary>
    public Guid RequestedByPathologistId { get; private set; }

    /// <summary>The colleague asked for an opinion.</summary>
    public Guid ConsultantPathologistId { get; private set; }

    /// <summary>What is being asked. A consultation without a question is not one.</summary>
    public string Question { get; private set; } = null!;

    public DateTimeOffset RequestedAtUtc { get; private set; }

    public string? Opinion { get; private set; }

    public DateTimeOffset? RespondedAtUtc { get; private set; }

    public bool IsAnswered => Opinion is not null;

    internal static Consultation Request(
        Guid pathologyCaseId,
        Guid requestedByPathologistId,
        Guid consultantPathologistId,
        string question,
        IClock clock)
    {
        if (requestedByPathologistId == Guid.Empty || consultantPathologistId == Guid.Empty)
        {
            throw new DomainException("Both the requesting and the consultant pathologist are required.");
        }

        if (requestedByPathologistId == consultantPathologistId)
        {
            throw new DomainException("A pathologist cannot consult themselves.");
        }

        if (string.IsNullOrWhiteSpace(question))
        {
            throw new DomainException("A consultation must state the question being asked.");
        }

        return new Consultation(
            Guid.NewGuid(),
            pathologyCaseId,
            requestedByPathologistId,
            consultantPathologistId,
            question.Trim(),
            clock.UtcNow);
    }

    /// <summary>
    /// The consultant records their opinion. Only the named consultant may do
    /// this: an opinion attributed to the wrong pathologist is a medico-legal
    /// problem, not a cosmetic one.
    /// </summary>
    internal void Respond(Guid respondingPathologistId, string opinion, IClock clock)
    {
        if (respondingPathologistId != ConsultantPathologistId)
        {
            throw new DomainException(
                "Only the pathologist the case was referred to may record the consultation opinion.");
        }

        if (IsAnswered)
        {
            throw new DomainException("This consultation has already been answered.");
        }

        if (string.IsNullOrWhiteSpace(opinion))
        {
            throw new DomainException("The consultation opinion cannot be empty.");
        }

        Opinion = opinion.Trim();
        RespondedAtUtc = clock.UtcNow;
    }
}
