using MediatR;

namespace VirtualHospital.SharedKernel.Primitives;

/// <summary>
/// A domain event raised inside a bounded context.
/// Domain events never leave the context; they are translated into
/// integration events (see VirtualHospital.Contracts) at the boundary.
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTimeOffset OccurredOnUtc { get; }
}
