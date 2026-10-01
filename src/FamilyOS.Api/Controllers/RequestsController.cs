using FamilyOS.Application.Requests;
using FamilyOS.Domain.Approvals;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/requests")]
[Authorize]
public class RequestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public RequestsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("types")]
    public async Task<ActionResult<List<RequestTypeDto>>> Types(CancellationToken ct)
        => Ok(await _mediator.Send(new GetRequestTypesQuery(), ct));

    [HttpGet("mine")]
    public async Task<ActionResult<List<RequestDto>>> Mine(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyRequestsQuery(), ct));

    [HttpGet("approval-queue")]
    public async Task<ActionResult<List<RequestDto>>> ApprovalQueue(CancellationToken ct)
        => Ok(await _mediator.Send(new GetApprovalQueueQuery(), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RequestDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetRequestQuery(id), ct));

    [HttpPost]
    public async Task<ActionResult<RequestDto>> Create([FromBody] CreateRequestCommand cmd, CancellationToken ct)
        => Ok(await _mediator.Send(cmd, ct));

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<RequestDto>> Submit(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new SubmitRequestCommand(id), ct));

    [HttpPost("{id:guid}/ask-question")]
    public async Task<ActionResult<RequestDto>> AskQuestion(Guid id, [FromBody] QuestionBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new AskQuestionCommand(id, body.QuestionText), ct));

    [HttpPost("{id:guid}/answer")]
    public async Task<ActionResult<RequestDto>> Answer(Guid id, [FromBody] AnswerBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new AnswerQuestionCommand(id, body.QuestionId, body.Answer), ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<RequestDto>> Approve(Guid id, [FromBody] ApproveBody? body, CancellationToken ct)
        => Ok(await _mediator.Send(new ApproveRequestCommand(id, body?.Conditional ?? false), ct));

    [HttpPost("{id:guid}/deny")]
    public async Task<ActionResult<RequestDto>> Deny(Guid id, [FromBody] DenyBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new DenyRequestCommand(id, body.Reason), ct));

    [HttpGet("{id:guid}/policy")]
    public async Task<ActionResult<PolicyEvaluationResult>> EvaluatePolicy(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new EvaluatePolicyQuery(id), ct));

    [HttpPost("{id:guid}/execution-plan")]
    public async Task<ActionResult<ExecutionPlanDto>> GeneratePlan(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GenerateExecutionPlanCommand(id), ct));

    [HttpPost("execution-plans/{planId:guid}/commit")]
    public async Task<ActionResult<ExecutionPlanDto>> CommitPlan(Guid planId, [FromBody] CommitBody? body, CancellationToken ct)
        => Ok(await _mediator.Send(new CommitExecutionPlanCommand(planId, body?.DeselectedItemIds), ct));

    public record QuestionBody(string QuestionText);
    public record AnswerBody(Guid QuestionId, string Answer);
    public record ApproveBody(bool Conditional);
    public record DenyBody(string Reason);
    public record CommitBody(List<Guid>? DeselectedItemIds);
}
