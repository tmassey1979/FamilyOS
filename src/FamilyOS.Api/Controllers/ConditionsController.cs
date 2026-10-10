using FamilyOS.Application.Conditions;
using FamilyOS.Domain.Common;
using FamilyOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/conditions")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public class ConditionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ConditionsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("pending")]
    public async Task<ActionResult<List<ConditionDto>>> Pending(CancellationToken ct)
        => Ok(await _mediator.Send(new GetPendingConditionsQuery(), ct));

    [HttpGet("by-request/{requestId:guid}")]
    public async Task<ActionResult<List<ConditionDto>>> ByRequest(Guid requestId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetConditionsForRequestQuery(requestId), ct));

    [HttpPost]
    [Authorize(Policy = FamilyAuthPolicies.AdultOrOwner)]
    public async Task<ActionResult<ConditionDto>> Create([FromBody] CreateConditionBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new CreateConditionCommand(
            body.Name,
            body.Type,
            body.RequestId,
            body.TaskId,
            body.Description,
            body.DeadlineUtc,
            body.LogicGroup ?? ConditionLogic.All,
            body.GroupOrder ?? 0), ct));

    [HttpPost("{id:guid}/satisfy")]
    public async Task<ActionResult<ConditionDto>> Satisfy(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new SatisfyConditionCommand(id), ct));

    [HttpPost("{id:guid}/fail")]
    [Authorize(Policy = FamilyAuthPolicies.AdultOrOwner)]
    public async Task<ActionResult<ConditionDto>> Fail(Guid id, [FromBody] FailBody? body, CancellationToken ct)
        => Ok(await _mediator.Send(new FailConditionCommand(id, body?.Reason), ct));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = FamilyAuthPolicies.AdultOrOwner)]
    public async Task<ActionResult<ConditionDto>> Cancel(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new CancelConditionCommand(id), ct));

    public record CreateConditionBody(
        string Name,
        ConditionType Type,
        Guid? RequestId,
        Guid? TaskId,
        string? Description,
        DateTime? DeadlineUtc,
        ConditionLogic? LogicGroup,
        int? GroupOrder);

    public record FailBody(string? Reason);
}
