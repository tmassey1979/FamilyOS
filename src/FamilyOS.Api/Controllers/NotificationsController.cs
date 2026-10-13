using FamilyOS.Application.Notifications;
using FamilyOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> Mine([FromQuery] bool unreadOnly = false, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetMyNotificationsQuery(unreadOnly), ct));

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<NotificationDto>> MarkRead(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new MarkNotificationReadCommand(id), ct));

    [HttpPost("read-all")]
    public async Task<ActionResult<object>> MarkAllRead(CancellationToken ct)
    {
        var count = await _mediator.Send(new MarkAllNotificationsReadCommand(), ct);
        return Ok(new { marked = count });
    }
}
