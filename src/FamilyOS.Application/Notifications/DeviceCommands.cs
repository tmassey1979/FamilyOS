using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Notifications;

public record DeviceRegistrationDto(
    Guid Id,
    string Platform,
    string? DeviceName,
    DateTime LastSeenAtUtc,
    bool IsActive);

public record RegisterDeviceCommand(
    string Token,
    string Platform,
    string? DeviceName = null) : IRequest<DeviceRegistrationDto>;

public record UnregisterDeviceCommand(string Token) : IRequest;
public record GetMyDevicesQuery() : IRequest<List<DeviceRegistrationDto>>;

public class DeviceHandlers :
    IRequestHandler<RegisterDeviceCommand, DeviceRegistrationDto>,
    IRequestHandler<UnregisterDeviceCommand>,
    IRequestHandler<GetMyDevicesQuery, List<DeviceRegistrationDto>>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public DeviceHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<DeviceRegistrationDto> Handle(RegisterDeviceCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || !_current.FamilyId.HasValue || !_current.MemberId.HasValue)
            throw new ForbiddenException("Not authenticated.");

        var token = cmd.Token.Trim();
        var existing = await _db.DeviceRegistrations
            .FirstOrDefaultAsync(d => d.Token == token && d.FamilyId == _current.FamilyId, ct);

        if (existing is not null)
        {
            existing.Touch(cmd.Platform, cmd.DeviceName);
            await _db.SaveChangesAsync(ct);
            return ToDto(existing);
        }

        var reg = DeviceRegistration.Create(
            _current.FamilyId.Value,
            _current.MemberId.Value,
            token,
            cmd.Platform,
            cmd.DeviceName);
        _db.DeviceRegistrations.Add(reg);
        await _db.SaveChangesAsync(ct);
        return ToDto(reg);
    }

    public async Task Handle(UnregisterDeviceCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || !_current.MemberId.HasValue)
            throw new ForbiddenException("Not authenticated.");

        var reg = await _db.DeviceRegistrations
            .FirstOrDefaultAsync(
                d => d.Token == cmd.Token.Trim() && d.MemberId == _current.MemberId, ct);
        if (reg is null) return;
        reg.Deactivate();
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<DeviceRegistrationDto>> Handle(GetMyDevicesQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || !_current.MemberId.HasValue)
            throw new ForbiddenException("Not authenticated.");

        var list = await _db.DeviceRegistrations.AsNoTracking()
            .Where(d => d.MemberId == _current.MemberId && d.IsActive)
            .OrderByDescending(d => d.LastSeenAtUtc)
            .ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    private static DeviceRegistrationDto ToDto(DeviceRegistration d) =>
        new(d.Id, d.Platform, d.DeviceName, d.LastSeenAtUtc, d.IsActive);
}
