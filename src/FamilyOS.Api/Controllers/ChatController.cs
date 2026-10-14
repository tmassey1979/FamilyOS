using FamilyOS.Application.Chat;
using FamilyOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public class ChatController : ControllerBase
{
    private readonly IMediator _mediator;

    public ChatController(IMediator mediator) => _mediator = mediator;

    [HttpGet("channel")]
    public async Task<ActionResult<ChatChannelDto>> Channel(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetFamilyChannelQuery(), ct));

    [HttpGet("messages")]
    public async Task<ActionResult<List<ChatMessageDto>>> Messages(
        [FromQuery] Guid? channelId,
        [FromQuery] int take = 50,
        [FromQuery] DateTime? beforeUtc = null,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetChatMessagesQuery(channelId, take, beforeUtc), ct));

    [HttpPost("messages")]
    public async Task<ActionResult<ChatMessageDto>> Send([FromBody] SendBody body, CancellationToken ct) =>
        Ok(await _mediator.Send(new SendChatMessageCommand(body.Body, body.ChannelId), ct));

    public record SendBody(string Body, Guid? ChannelId = null);
}
