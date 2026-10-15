using FamilyOS.Application.Family;
using FamilyOS.Domain.Common;
using FamilyOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/invites")]
public class InvitesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InvitesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Preview an invite (auth optional for family name).</summary>
    [HttpGet("{code}")]
    [AllowAnonymous]
    public async Task<ActionResult<InvitePreviewDto>> Preview(string code, CancellationToken ct)
        => Ok(await _mediator.Send(new GetInvitePreviewQuery(code), ct));

    [HttpPost]
    [Authorize(Policy = FamilyAuthPolicies.AdultOrOwner)]
    public async Task<ActionResult<InviteDto>> Create([FromBody] CreateInviteBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new CreateInviteCommand(body.Role, body.EmailHint, body.ValidDays ?? 14), ct));

    [HttpGet]
    [Authorize(Policy = FamilyAuthPolicies.AdultOrOwner)]
    public async Task<ActionResult<List<InviteDto>>> List(CancellationToken ct)
        => Ok(await _mediator.Send(new ListInvitesQuery(), ct));

    [HttpPost("{code}/accept")]
    [Authorize]
    public async Task<ActionResult<MemberDto>> Accept(string code, [FromBody] AcceptInviteBody? body, CancellationToken ct)
        => Ok(await _mediator.Send(new AcceptInviteCommand(code, body?.DisplayName), ct));

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = FamilyAuthPolicies.AdultOrOwner)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RevokeInviteCommand(id), ct);
        return NoContent();
    }

    public record CreateInviteBody(FamilyRole Role, string? EmailHint = null, int? ValidDays = 14);
    public record AcceptInviteBody(string? DisplayName);
}
