using FamilyOS.Application.Tasks;
using FamilyOS.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly IMediator _mediator;

    public TasksController(IMediator mediator) => _mediator = mediator;

    [HttpGet("mine")]
    public async Task<ActionResult<List<TaskDto>>> GetMine([FromQuery] FamilyTaskStatus? status, CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyTasksQuery(status), ct));

    [HttpGet]
    public async Task<ActionResult<List<TaskDto>>> GetFamily([FromQuery] FamilyTaskStatus? status, CancellationToken ct)
        => Ok(await _mediator.Send(new GetFamilyTasksQuery(status), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetTaskQuery(id), ct));

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<List<TaskHistoryDto>>> History(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetTaskHistoryQuery(id), ct));

    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create([FromBody] CreateTaskCommand cmd, CancellationToken ct)
        => Ok(await _mediator.Send(cmd, ct));

    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<TaskDto>> Assign(Guid id, [FromBody] AssignBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new AssignTaskCommand(id, body.AssignToMemberId), ct));

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<TaskDto>> Accept(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new AcceptTaskCommand(id), ct));

    [HttpPost("{id:guid}/decline")]
    public async Task<ActionResult<TaskDto>> Decline(Guid id, [FromBody] DeclineBody? body, CancellationToken ct)
        => Ok(await _mediator.Send(new DeclineTaskCommand(id, body?.ReasonId, body?.Note), ct));

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<TaskDto>> Start(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new StartTaskCommand(id), ct));

    [HttpPost("{id:guid}/pause")]
    public async Task<ActionResult<TaskDto>> Pause(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new PauseTaskCommand(id), ct));

    [HttpPost("{id:guid}/resume")]
    public async Task<ActionResult<TaskDto>> Resume(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ResumeTaskCommand(id), ct));

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<TaskDto>> Complete(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new CompleteTaskCommand(id), ct));

    [HttpPost("{id:guid}/defer")]
    public async Task<ActionResult<TaskDto>> Defer(Guid id, [FromBody] NoteBody? body, CancellationToken ct)
        => Ok(await _mediator.Send(new DeferTaskCommand(id, body?.Note), ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<TaskDto>> Cancel(Guid id, [FromBody] NoteBody? body, CancellationToken ct)
        => Ok(await _mediator.Send(new CancelTaskCommand(id, body?.Reason), ct));

    [HttpPost("{id:guid}/reassign")]
    public async Task<ActionResult<TaskDto>> Reassign(Guid id, [FromBody] AssignBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new ReassignTaskCommand(id, body.AssignToMemberId), ct));

    public record AssignBody(Guid AssignToMemberId);
    public record DeclineBody(Guid? ReasonId, string? Note);
    public record NoteBody(string? Note, string? Reason);
}
