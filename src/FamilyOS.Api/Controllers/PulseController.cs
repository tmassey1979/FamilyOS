using FamilyOS.Application.Pulse;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/pulse")]
[Authorize]
public class PulseController : ControllerBase
{
    private readonly IMediator _mediator;

    public PulseController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<PulseDto>> Get(CancellationToken ct)
        => Ok(await _mediator.Send(new GetPulseQuery(), ct));
}
