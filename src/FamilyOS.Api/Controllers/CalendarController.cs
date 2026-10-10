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

    [HttpPost]
    public async Task<ActionResult<CalendarEventDto>> Create([FromBody] CreateBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new CreateCalendarEventCommand(
            body.Title,
            body.StartUtc,
            body.EndUtc,
            body.AllDay,
            body.Location,
            body.Description,
            body.LinkedTaskId,
            body.LinkedRequestId), ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CalendarEventDto>> Update(Guid id, [FromBody] UpdateBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateCalendarEventCommand(
            id, body.Title, body.StartUtc, body.EndUtc, body.AllDay, body.Location), ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new CancelCalendarEventCommand(id), ct);
        return NoContent();
    }

    public record CreateBody(
        string Title,
        DateTime StartUtc,
        DateTime? EndUtc,
        bool AllDay,
        string? Location,
        string? Description,
        Guid? LinkedTaskId,
        Guid? LinkedRequestId);

    public record UpdateBody(
        string Title,
        DateTime StartUtc,
        DateTime? EndUtc,
        bool AllDay,
        string? Location);
}
