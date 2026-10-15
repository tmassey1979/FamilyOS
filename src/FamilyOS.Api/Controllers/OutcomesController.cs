using FamilyOS.Application.Pulse;
using FamilyOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/outcomes")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public class OutcomesController : ControllerBase
{
    private readonly IMediator _mediator;

    public OutcomesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<OutcomesDto>> Get(CancellationToken ct)
        => Ok(await _mediator.Send(new GetOutcomesQuery(), ct));
}
