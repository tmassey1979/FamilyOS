using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Notifications;

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Body,
    Guid? RelatedEntityId,
    string? RelatedEntityType,
    bool IsRead,
    DateTime? ReadAtUtc,
    DateTime CreatedAtUtc);

public record GetMyNotificationsQuery(bool UnreadOnly = false) : IRequest<List<NotificationDto>>;
public record MarkNotificationReadCommand(Guid NotificationId) : IRequest<NotificationDto>;
public record MarkAllNotificationsReadCommand() : IRequest<int>;
public record CreateNotificationCommand(
    Guid RecipientMemberId,
    NotificationType Type,
    string Title,
    string Body,
    Guid? RelatedEntityId = null,
    string? RelatedEntityType = null) : IRequest<NotificationDto>;

public class NotificationHandlers :
    IRequestHandler<GetMyNotificationsQuery, List<NotificationDto>>,
    IRequestHandler<MarkNotificationReadCommand, NotificationDto>,
    IRequestHandler<MarkAllNotificationsReadCommand, int>,
    IRequestHandler<CreateNotificationCommand, NotificationDto>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public NotificationHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<NotificationDto>> Handle(GetMyNotificationsQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || !_current.MemberId.HasValue || !_current.FamilyId.HasValue)
            throw new ForbiddenException("Not authenticated.");

        var memberId = _current.MemberId.Value;
        var familyId = _current.FamilyId.Value;

        var q = _db.Notifications.AsNoTracking()
            .Where(n => n.FamilyId == familyId && n.RecipientMemberId == memberId);

        if (query.UnreadOnly)
            q = q.Where(n => !n.IsRead);

        var list = await q
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);

        return list.Select(ToDto).ToList();
    }

    public async Task<NotificationDto> Handle(MarkNotificationReadCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || !_current.MemberId.HasValue)
            throw new ForbiddenException("Not authenticated.");

        var n = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == cmd.NotificationId && x.RecipientMemberId == _current.MemberId, ct)
            ?? throw new NotFoundException("Notification", cmd.NotificationId);

        n.MarkRead();
        await _db.SaveChangesAsync(ct);
        return ToDto(n);
    }

    public async Task<int> Handle(MarkAllNotificationsReadCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || !_current.MemberId.HasValue)
            throw new ForbiddenException("Not authenticated.");

        var unread = await _db.Notifications
            .Where(n => n.RecipientMemberId == _current.MemberId && !n.IsRead)
            .ToListAsync(ct);

        foreach (var n in unread)
            n.MarkRead();

        await _db.SaveChangesAsync(ct);
        return unread.Count;
    }

    public async Task<NotificationDto> Handle(CreateNotificationCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || !_current.FamilyId.HasValue)
            throw new ForbiddenException("Not authenticated.");

        var n = Notification.Create(
            _current.FamilyId.Value,
            cmd.RecipientMemberId,
            cmd.Type,
            cmd.Title,
            cmd.Body,
            cmd.RelatedEntityId,
            cmd.RelatedEntityType);

        _db.Notifications.Add(n);
        await _db.SaveChangesAsync(ct);
        return ToDto(n);
    }

    private static NotificationDto ToDto(Notification n) =>
        new(n.Id, n.Type.ToString(), n.Title, n.Body, n.RelatedEntityId, n.RelatedEntityType,
            n.IsRead, n.ReadAtUtc, n.CreatedAtUtc);
}
