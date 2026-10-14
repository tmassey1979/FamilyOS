using FamilyOS.Application.Interfaces;
using FamilyOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FamilyOS.Api.Hubs;

/// <summary>Realtime family chat. Clients join group family:{familyId}.</summary>
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public class FamilyChatHub : Hub
{
    private readonly ICurrentUserService _current;

    public FamilyChatHub(ICurrentUserService current) => _current = current;

    public override async Task OnConnectedAsync()
    {
        await _current.EnsureLoadedAsync();
        if (_current.FamilyId is Guid familyId)
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(familyId));
        await base.OnConnectedAsync();
    }

    public async Task JoinFamily()
    {
        await _current.EnsureLoadedAsync();
        if (_current.FamilyId is Guid familyId)
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(familyId));
    }

    public static string GroupName(Guid familyId) => $"family:{familyId:N}";
}

public sealed class SignalRFamilyChatNotifier : IFamilyChatNotifier
{
    private readonly IHubContext<FamilyChatHub> _hub;

    public SignalRFamilyChatNotifier(IHubContext<FamilyChatHub> hub) => _hub = hub;

    public Task MessageSentAsync(Guid familyId, ChatMessageRealtimeDto message, CancellationToken ct = default) =>
        _hub.Clients.Group(FamilyChatHub.GroupName(familyId))
            .SendAsync("messageReceived", message, ct);
}
