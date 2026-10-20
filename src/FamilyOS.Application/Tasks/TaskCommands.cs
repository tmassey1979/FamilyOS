using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Conditions;
using FamilyOS.Domain.Notifications;
using FamilyOS.Domain.Tasks;
using FamilyOS.Domain.TimeIntelligence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Tasks;

public record TaskDto(
    Guid Id, string Title, string? Description, FamilyTaskStatus Status, TaskPriority Priority,
    Guid? AssignedToMemberId, string? AssignedToName, DateTime? DueDate,
    int? EstimatedDurationMinutes, int? ActualDurationMinutes, string? Category,
    bool CalendarBacked, DateTime CreatedAtUtc, bool IsOverdue,
    string? PlannedStartTime, string? PlannedEndTime,
    int? VarianceMinutes,
    Guid? RecurrenceDefinitionId = null,
    bool IsRecurring = false,
    string? RecurrenceFrequency = null);

public record TaskHistoryDto(Guid Id, FamilyTaskStatus Status, Guid ActorMemberId, string Detail, DateTime TimestampUtc);

public record RecurrenceDto(
    Guid Id, string Title, string? Description, RecurrenceFrequency Frequency, int Interval,
    DayOfWeek? DayOfWeek, Guid? DefaultAssigneeMemberId, DeclinePolicy DeclinePolicy,
    bool IsActive, DateTime? NextOccurrenceUtc, string? Category);

public record CreateTaskCommand(
    string Title, string? Description, Guid? AssignToMemberId, TaskPriority Priority,
    DateTime? DueDate, string? Category, bool CalendarBacked, int? EstimatedDurationMinutes,
    RecurrenceFrequency? Frequency = null,
    int Interval = 1,
    DayOfWeek? DayOfWeek = null
) : IRequest<TaskDto>;

public record CreateRecurrenceCommand(
    string Title,
    RecurrenceFrequency Frequency,
    int Interval = 1,
    DayOfWeek? DayOfWeek = null,
    Guid? DefaultAssigneeMemberId = null,
    string? Category = null,
    string? Description = null,
    DeclinePolicy DeclinePolicy = DeclinePolicy.NoChange,
    bool GenerateFirstInstance = true
) : IRequest<RecurrenceDto>;

public record GetRecurrencesQuery() : IRequest<List<RecurrenceDto>>;
public record DeactivateRecurrenceCommand(Guid RecurrenceId) : IRequest<RecurrenceDto>;

public record AssignTaskCommand(Guid TaskId, Guid AssignToMemberId) : IRequest<TaskDto>;
public record AcceptTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record DeclineTaskCommand(Guid TaskId, Guid? ReasonId, string? Note) : IRequest<TaskDto>;
public record StartTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record PauseTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record ResumeTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record CompleteTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record DeferTaskCommand(Guid TaskId, string? Note) : IRequest<TaskDto>;
public record CancelTaskCommand(Guid TaskId, string? Reason) : IRequest<TaskDto>;
public record SkipTaskCommand(Guid TaskId, string? Note) : IRequest<TaskDto>;
public record ReassignTaskCommand(Guid TaskId, Guid NewAssigneeMemberId) : IRequest<TaskDto>;
public record GetTaskQuery(Guid TaskId) : IRequest<TaskDto>;
public record GetMyTasksQuery(FamilyTaskStatus? Status = null) : IRequest<List<TaskDto>>;
public record GetFamilyTasksQuery(FamilyTaskStatus? Status = null) : IRequest<List<TaskDto>>;
public record GetTaskHistoryQuery(Guid TaskId) : IRequest<List<TaskHistoryDto>>;
public record UpdateTaskDetailsCommand(
    Guid TaskId,
    string? Title = null,
    string? Description = null,
    TaskPriority? Priority = null,
    string? Category = null,
    DateTime? DueDate = null,
    bool ClearDueDate = false
) : IRequest<TaskDto>;

public record SetPlannedTimesCommand(
    Guid TaskId,
    string? PlannedStartTime = null,
    string? PlannedEndTime = null,
    int? EstimatedDurationMinutes = null
) : IRequest<TaskDto>;
