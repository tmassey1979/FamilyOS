using System.Security.Claims;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
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

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.ExternalIdentityId == externalId, ct);

        if (user is null)
        {
            var principal = _http.HttpContext?.User;
            var email = principal?.FindFirstValue(ClaimTypes.Email)
                ?? principal?.FindFirstValue("email")
                ?? $"{externalId}@familyos.local";
            var given = principal?.FindFirstValue(ClaimTypes.GivenName)
                ?? principal?.FindFirstValue("given_name")
                ?? principal?.FindFirstValue("preferred_username")
                ?? "Member";
            var familyName = principal?.FindFirstValue(ClaimTypes.Surname)
                ?? principal?.FindFirstValue("family_name")
                ?? "";

            user = User.Create(externalId, email, given, familyName);
            _db.Users.Add(user);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch
            {
                user = await _db.Users.FirstOrDefaultAsync(u => u.ExternalIdentityId == externalId, ct);
                if (user is null) return;
            }
        }

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
