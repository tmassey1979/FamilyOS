using FamilyOS.Application.Calendar;
using FamilyOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/calendar")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public class CalendarController : ControllerBase
{
    private readonly IMediator _mediator;

    public CalendarController(IMediator mediator) => _mediator = mediator;

    [HttpGet("upcoming")]
    public async Task<ActionResult<List<CalendarEventDto>>> Upcoming([FromQuery] int days = 14, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetUpcomingEventsQuery(days), ct));

    [HttpGet]
    public async Task<ActionResult<List<CalendarEventDto>>> Range(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetEventsRangeQuery(from, to), ct));
}
