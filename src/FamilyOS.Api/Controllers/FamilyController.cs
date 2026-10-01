using FamilyOS.Application.Family;
using MediatR;
using FamilyOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/family")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public class FamilyController : ControllerBase
{
    private readonly IMediator _mediator;

    public FamilyController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<FamilyDto>> Get(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyFamilyQuery(), ct));

    [HttpGet("members")]
    public async Task<ActionResult<List<MemberDto>>> Members(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMembersQuery(), ct));

    [HttpGet("decline-reasons")]
    public async Task<ActionResult<List<DeclineReasonDto>>> DeclineReasons(CancellationToken ct)
        => Ok(await _mediator.Send(new GetDeclineReasonsQuery(), ct));
}
