using System.Security.Claims;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;
    private readonly FamilyOsDbContext _db;
    private bool _loaded;
    private Guid? _userId;
    private Guid? _familyId;
    private Guid? _memberId;
    private FamilyRole? _role;

    public CurrentUserService(IHttpContextAccessor http, FamilyOsDbContext db)
    {
        _http = http;
        _db = db;
    }

    public string? ExternalIdentityId =>
        _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _http.HttpContext?.User?.FindFirstValue("sub");

    public Guid? UserId => _userId;
    public Guid? FamilyId => _familyId;
    public Guid? MemberId => _memberId;
    public FamilyRole? Role => _role;
    public bool IsAuthenticated => !string.IsNullOrEmpty(ExternalIdentityId) && _userId.HasValue;

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_loaded) return;
        _loaded = true;

        var externalId = ExternalIdentityId;
        if (string.IsNullOrEmpty(externalId)) return;

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.ExternalIdentityId == externalId, ct);

        if (user == null) return;

        _userId = user.Id;
        _familyId = user.FamilyId;
        _memberId = user.FamilyMemberId;

        if (user.FamilyMemberId.HasValue)
        {
            var member = await _db.FamilyMembers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == user.FamilyMemberId, ct);
            _role = member?.Role;
        }
    }

    public async Task<(Guid FamilyId, Guid MemberId, FamilyRole Role)> RequireFamilyAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        if (!IsAuthenticated || !FamilyId.HasValue || !MemberId.HasValue)
            throw new FamilyOS.Application.Common.ForbiddenException("Not a family member.");
        return (FamilyId.Value, MemberId.Value, Role ?? FamilyRole.Child);
    }
}
