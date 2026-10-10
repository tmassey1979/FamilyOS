using FamilyOS.Application.Family;
using FamilyOS.Domain.Common;
using FamilyOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/family")]
public class FamilyController : ControllerBase
{
    private readonly IMediator _mediator;

    public FamilyController(IMediator mediator) => _mediator = mediator;

    /// <summary>Onboarding: create a new household. Requires auth but not existing family membership.</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<FamilyDto>> Create([FromBody] CreateFamilyBody body, CancellationToken ct)
    {
        var dto = await _mediator.Send(new CreateFamilyCommand(
            body.Name,
            body.TimeZone,
            body.Currency,
            body.OwnerDisplayName ?? "Owner",
            body.OwnerEmail ?? "owner@familyos.local",
            body.OwnerFirstName,
            body.OwnerLastName), ct);
        return CreatedAtAction(nameof(Get), dto);
    }

    [HttpGet]
    [Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
    public async Task<ActionResult<FamilyDto>> Get(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyFamilyQuery(), ct));

    [HttpGet("members")]
    [Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
    public async Task<ActionResult<List<MemberDto>>> Members(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMembersQuery(), ct));

    [HttpPost("members")]
    [Authorize(Policy = FamilyAuthPolicies.AdultOrOwner)]
    public async Task<ActionResult<MemberDto>> AddMember([FromBody] AddMemberBody body, CancellationToken ct)
    {
        var dto = await _mediator.Send(new AddFamilyMemberCommand(
            body.Email,
            body.DisplayName,
            body.Role,
            body.ExternalIdentityId,
            body.FirstName,
            body.LastName), ct);
        return CreatedAtAction(nameof(Members), dto);
    }


    [HttpPost("members/{id:guid}/change-role")]
    [Authorize(Policy = FamilyAuthPolicies.OwnerOnly)]
    public async Task<ActionResult<MemberDto>> ChangeRole(Guid id, [FromBody] ChangeRoleBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new ChangeMemberRoleCommand(id, body.Role), ct));

    [HttpPost("members/{id:guid}/deactivate")]
    [Authorize(Policy = FamilyAuthPolicies.OwnerOnly)]
    public async Task<ActionResult<MemberDto>> Deactivate(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new DeactivateMemberCommand(id), ct));

    [HttpGet("decline-reasons")]
    [Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
    public async Task<ActionResult<List<DeclineReasonDto>>> DeclineReasons(CancellationToken ct)
        => Ok(await _mediator.Send(new GetDeclineReasonsQuery(), ct));

    public record CreateFamilyBody(
        string Name,
        string? TimeZone,
        string? Currency,
        string? OwnerDisplayName,
        string? OwnerEmail,
        string? OwnerFirstName,
        string? OwnerLastName);

    public record AddMemberBody(
        string Email,
        string DisplayName,
        FamilyRole Role,
        string? ExternalIdentityId,
        string? FirstName,
        string? LastName);
}
