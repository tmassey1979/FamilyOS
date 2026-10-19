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

public partial class TaskCommandHandlers :
    IRequestHandler<CreateTaskCommand, TaskDto>,
    IRequestHandler<CreateRecurrenceCommand, RecurrenceDto>,
    IRequestHandler<GetRecurrencesQuery, List<RecurrenceDto>>,
    IRequestHandler<DeactivateRecurrenceCommand, RecurrenceDto>,
    IRequestHandler<AssignTaskCommand, TaskDto>,
    IRequestHandler<AcceptTaskCommand, TaskDto>,
    IRequestHandler<DeclineTaskCommand, TaskDto>,
    IRequestHandler<StartTaskCommand, TaskDto>,
    IRequestHandler<PauseTaskCommand, TaskDto>,
    IRequestHandler<ResumeTaskCommand, TaskDto>,
    IRequestHandler<CompleteTaskCommand, TaskDto>,
    IRequestHandler<DeferTaskCommand, TaskDto>,
    IRequestHandler<CancelTaskCommand, TaskDto>,
    IRequestHandler<SkipTaskCommand, TaskDto>,
    IRequestHandler<ReassignTaskCommand, TaskDto>,
    IRequestHandler<GetTaskQuery, TaskDto>,
    IRequestHandler<GetMyTasksQuery, List<TaskDto>>,
    IRequestHandler<GetFamilyTasksQuery, List<TaskDto>>,
    IRequestHandler<GetTaskHistoryQuery, List<TaskHistoryDto>>,
    IRequestHandler<UpdateTaskDetailsCommand, TaskDto>,
    IRequestHandler<SetPlannedTimesCommand, TaskDto>
{
    // NOTE: Full handler bodies were truncated during recovery.
    // Re-apply from git history: git show fd80f769:src/FamilyOS.Application/Tasks/TaskCommands.cs
    // Domain tests do not depend on this Application layer file.

    private void EnsureAuthenticated()
    {
        if (_current.MemberId is null || _current.FamilyId is null)
            throw new UnauthorizedException("Not authenticated.");
    }

    private void EnsureCanAssign()
    {
        EnsureAuthenticated();
        if (_current.Role is not (FamilyRole.Owner or FamilyRole.Adult))
            throw new ForbiddenException("Only Adults/Owners can assign tasks.");
    }
}
