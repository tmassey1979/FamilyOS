namespace FamilyOS.Contracts.Events;

public record TaskCreated(Guid TaskId, Guid FamilyId, string Title, Guid CreatedByMemberId, DateTime OccurredAtUtc);
public record TaskAssigned(Guid TaskId, Guid FamilyId, Guid AssignedToMemberId, Guid AssignedByMemberId, DateTime OccurredAtUtc);
public record TaskAccepted(Guid TaskId, Guid FamilyId, Guid MemberId, DateTime OccurredAtUtc);
public record TaskStarted(Guid TaskId, Guid FamilyId, Guid MemberId, DateTime OccurredAtUtc);
public record TaskPaused(Guid TaskId, Guid FamilyId, Guid MemberId, DateTime OccurredAtUtc);
public record TaskResumed(Guid TaskId, Guid FamilyId, Guid MemberId, DateTime OccurredAtUtc);
public record TaskCompleted(Guid TaskId, Guid FamilyId, Guid MemberId, int? ActualDurationMinutes, DateTime OccurredAtUtc);
public record TaskDeclined(Guid TaskId, Guid FamilyId, Guid MemberId, Guid? ReasonId, string? Note, DateTime OccurredAtUtc);
public record TaskReassigned(Guid TaskId, Guid FamilyId, Guid FromMemberId, Guid ToMemberId, Guid ByMemberId, DateTime OccurredAtUtc);
public record TaskCancelled(Guid TaskId, Guid FamilyId, Guid ByMemberId, string? Reason, DateTime OccurredAtUtc);
