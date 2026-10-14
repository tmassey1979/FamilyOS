using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Chat;

public record ChatChannelDto(Guid Id, string Name, string Kind);

public record ChatMessageDto(
    Guid Id,
    Guid ChannelId,
    Guid SenderMemberId,
    string SenderName,
    string Body,
    DateTime SentAtUtc);

public record GetFamilyChannelQuery() : IRequest<ChatChannelDto>;
public record GetChatMessagesQuery(Guid? ChannelId, int Take = 50, DateTime? BeforeUtc = null) : IRequest<List<ChatMessageDto>>;
public record SendChatMessageCommand(string Body, Guid? ChannelId = null) : IRequest<ChatMessageDto>;

public class ChatHandlers :
    IRequestHandler<GetFamilyChannelQuery, ChatChannelDto>,
    IRequestHandler<GetChatMessagesQuery, List<ChatMessageDto>>,
    IRequestHandler<SendChatMessageCommand, ChatMessageDto>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFamilyChatNotifier _notifier;

    public ChatHandlers(IFamilyOsDbContext db, ICurrentUserService current, IFamilyChatNotifier notifier)
    {
        _db = db;
        _current = current;
        _notifier = notifier;
    }

    public async Task<ChatChannelDto> Handle(GetFamilyChannelQuery query, CancellationToken ct)
    {
        var (familyId, _, _) = await _current.RequireFamilyAsync(ct);
        var channel = await EnsureFamilyChannelAsync(familyId, ct);
        return new ChatChannelDto(channel.Id, channel.Name, channel.Kind.ToString());
    }

    public async Task<List<ChatMessageDto>> Handle(GetChatMessagesQuery query, CancellationToken ct)
    {
        var (familyId, _, _) = await _current.RequireFamilyAsync(ct);
        var channel = query.ChannelId.HasValue
            ? await _db.ChatChannels.FirstOrDefaultAsync(c => c.Id == query.ChannelId && c.FamilyId == familyId && c.IsActive, ct)
              ?? throw new NotFoundException("ChatChannel", query.ChannelId.Value)
            : await EnsureFamilyChannelAsync(familyId, ct);

        var take = Math.Clamp(query.Take, 1, 100);
        var q = _db.ChatMessages.AsNoTracking()
            .Where(m => m.ChannelId == channel.Id && m.FamilyId == familyId && !m.IsDeleted);

        if (query.BeforeUtc.HasValue)
            q = q.Where(m => m.SentAtUtc < query.BeforeUtc.Value);

        var messages = await q
            .OrderByDescending(m => m.SentAtUtc)
            .Take(take)
            .ToListAsync(ct);

        var senderIds = messages.Select(m => m.SenderMemberId).Distinct().ToList();
        var names = await _db.FamilyMembers.AsNoTracking()
            .Where(m => senderIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.DisplayName, ct);

        return messages
            .OrderBy(m => m.SentAtUtc)
            .Select(m => new ChatMessageDto(
                m.Id,
                m.ChannelId,
                m.SenderMemberId,
                names.GetValueOrDefault(m.SenderMemberId, "Member"),
                m.Body,
                m.SentAtUtc))
            .ToList();
    }

    public async Task<ChatMessageDto> Handle(SendChatMessageCommand cmd, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (string.IsNullOrWhiteSpace(cmd.Body))
            throw new DomainException("Message body is required.");

        var channel = cmd.ChannelId.HasValue
            ? await _db.ChatChannels.FirstOrDefaultAsync(c => c.Id == cmd.ChannelId && c.FamilyId == familyId && c.IsActive, ct)
              ?? throw new NotFoundException("ChatChannel", cmd.ChannelId.Value)
            : await EnsureFamilyChannelAsync(familyId, ct);

        var msg = ChatMessage.Create(familyId, channel.Id, memberId, cmd.Body);
        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync(ct);

        var senderName = await _db.FamilyMembers.AsNoTracking()
            .Where(m => m.Id == memberId)
            .Select(m => m.DisplayName)
            .FirstOrDefaultAsync(ct) ?? "Member";

        var dto = new ChatMessageDto(msg.Id, msg.ChannelId, msg.SenderMemberId, senderName, msg.Body, msg.SentAtUtc);

        await _notifier.MessageSentAsync(
            familyId,
            new ChatMessageRealtimeDto(dto.Id, dto.ChannelId, dto.SenderMemberId, dto.SenderName, dto.Body, dto.SentAtUtc),
            ct);

        return dto;
    }

    private async Task<ChatChannel> EnsureFamilyChannelAsync(Guid familyId, CancellationToken ct)
    {
        var existing = await _db.ChatChannels
            .FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Kind == ChatChannelKind.Family && c.IsActive, ct);
        if (existing is not null) return existing;

        var channel = ChatChannel.CreateFamilyChannel(familyId);
        _db.ChatChannels.Add(channel);
        await _db.SaveChangesAsync(ct);
        return channel;
    }
}
