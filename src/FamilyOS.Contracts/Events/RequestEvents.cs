namespace FamilyOS.Contracts.Events;

public record RequestCreated(Guid RequestId, Guid FamilyId, Guid RequesterMemberId, string Type, string Title, DateTime OccurredAtUtc);
public record RequestSubmitted(Guid RequestId, Guid FamilyId, Guid RequesterMemberId, DateTime OccurredAtUtc);
public record RequestQuestionAsked(Guid RequestId, Guid FamilyId, Guid AskedByMemberId, string Question, DateTime OccurredAtUtc);
public record RequestInformationProvided(Guid RequestId, Guid FamilyId, Guid AnsweredByMemberId, DateTime OccurredAtUtc);
public record RequestApproved(Guid RequestId, Guid FamilyId, Guid ApproverMemberId, bool Conditional, DateTime OccurredAtUtc);
public record RequestDenied(Guid RequestId, Guid FamilyId, Guid ApproverMemberId, string Reason, DateTime OccurredAtUtc);
public record RequestExecutable(Guid RequestId, Guid FamilyId, DateTime OccurredAtUtc);
public record ExecutionPlanCommitted(Guid PlanId, Guid RequestId, Guid FamilyId, Guid CommittedByMemberId, DateTime OccurredAtUtc);
