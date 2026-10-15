using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Family;

public record InviteDto(
    Guid Id,
    string Code,
    string Role,
    string? EmailHint,
    DateTime ExpiresAtUtc,
    bool IsOpen,
    DateTime? AcceptedAtUtc);

public record InvitePreviewDto(
    string Code,
    string FamilyName,
    string Role,
    DateTime ExpiresAtUtc,
    bool IsOpen);

public record CreateInviteCommand(
    FamilyRole Role = FamilyRole.Adult,
    string? EmailHint = null,
    int ValidDays = 14) : IRequest<InviteDto>;

public record ListInvitesQuery() : IRequest<List<InviteDto>>;
public record RevokeInviteCommand(Guid InviteId) : IRequest;
public record GetInvitePreviewQuery(string Code) : IRequest<InvitePreviewDto>;
public record AcceptInviteCommand(string Code, string? DisplayName = null) : IRequest<MemberDto>;

public class InviteCommandHandlers :
    IRequestHandler<CreateInviteCommand, InviteDto>,
    IRequestHandler<ListInvitesQuery, List<InviteDto>>,
    IRequestHandler<RevokeInviteCommand>,
    IRequestHandler<GetInvitePreviewQuery, InvitePreviewDto>,
    IRequestHandler<AcceptInviteCommand, MemberDto>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public InviteCommandHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<InviteDto> Handle(CreateInviteCommand cmd, CancellationToken ct)
    {
        var (familyId, memberId, role) = await _current.RequireFamilyAsync(ct);
        if (role is not (FamilyRole.Owner or FamilyRole.Adult))
            throw new ForbiddenException("Only Owner or Adult can create invites.");

        if (cmd.Role == FamilyRole.Owner)
            throw new ValidationException("Role", "Cannot invite as Owner.");

        var invite = FamilyInvite.Create(familyId, memberId, cmd.Role, cmd.EmailHint, cmd.ValidDays);
        _db.FamilyInvites.Add(invite);
        await _db.SaveChangesAsync(ct);
        return ToDto(invite);
    }

    public async Task<List<InviteDto>> Handle(ListInvitesQuery query, CancellationToken ct)
    {
        var (familyId, _, role) = await _current.RequireFamilyAsync(ct);
        if (role is not (FamilyRole.Owner or FamilyRole.Adult))
            throw new ForbiddenException("Only Owner or Adult can list invites.");

        var list = await _db.FamilyInvites.AsNoTracking()
            .Where(i => i.FamilyId == familyId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task Handle(RevokeInviteCommand cmd, CancellationToken ct)
    {
        var (familyId, _, role) = await _current.RequireFamilyAsync(ct);
        if (role is not (FamilyRole.Owner or FamilyRole.Adult))
            throw new ForbiddenException("Only Owner or Adult can revoke invites.");

        var invite = await _db.FamilyInvites
            .FirstOrDefaultAsync(i => i.Id == cmd.InviteId && i.FamilyId == familyId, ct)
            ?? throw new NotFoundException("FamilyInvite", cmd.InviteId);
        invite.Revoke();
        await _db.SaveChangesAsync(ct);
    }

    public async Task<InvitePreviewDto> Handle(GetInvitePreviewQuery query, CancellationToken ct)
    {
        var code = NormalizeCode(query.Code);
        var invite = await _db.FamilyInvites.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Code == code, ct)
            ?? throw new NotFoundException("Invite", code);

        var family = await _db.Families.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == invite.FamilyId, ct)
            ?? throw new NotFoundException("Family", invite.FamilyId);

        return new InvitePreviewDto(
            invite.Code,
            family.Name,
            invite.Role.ToString(),
            invite.ExpiresAtUtc,
            invite.IsOpen);
    }

    public async Task<MemberDto> Handle(AcceptInviteCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);

        var externalId = _current.ExternalIdentityId
            ?? throw new ForbiddenException("Sign in before accepting an invite.");

        if (_current.FamilyId.HasValue)
            throw new DomainException("You already belong to a family.");

        var code = NormalizeCode(cmd.Code);
        var invite = await _db.FamilyInvites
            .FirstOrDefaultAsync(i => i.Code == code, ct)
            ?? throw new NotFoundException("Invite", code);

        if (!invite.IsOpen)
            throw new DomainException("This invite is expired, revoked, or already used.");

        var family = await _db.Families
            .Include(f => f.Members)
            .FirstOrDefaultAsync(f => f.Id == invite.FamilyId, ct)
            ?? throw new NotFoundException("Family", invite.FamilyId);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.ExternalIdentityId == externalId, ct);
        if (user is null)
        {
            // Claims may not be fully available; create a stub user
            user = User.Create(
                externalId,
                invite.EmailHint ?? $"{externalId}@familyos.local",
                cmd.DisplayName?.Split(' ').FirstOrDefault() ?? "Member",
                "");
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
        }
        else if (user.FamilyId.HasValue)
        {
            throw new DomainException("This account is already linked to a family.");
        }

        var display = string.IsNullOrWhiteSpace(cmd.DisplayName)
            ? user.DisplayName
            : cmd.DisplayName.Trim();

        if (family.Members.Any(m => m.UserId == user.Id && m.IsActive))
            throw new DomainException("Already a member of this family.");

        var member = family.AddMember(user, invite.Role, display);
        user.AssignToFamily(family.Id, member.Id);
        invite.Accept(member.Id);
        await _db.SaveChangesAsync(ct);

        return new MemberDto(member.Id, user.Id, member.DisplayName, member.Role, member.IsActive);
    }

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationException("Code", "Invite code is required.");
        return code.Trim().ToUpperInvariant().Replace(" ", "");
    }

    private static InviteDto ToDto(FamilyInvite i) => new(
        i.Id, i.Code, i.Role.ToString(), i.EmailHint, i.ExpiresAtUtc, i.IsOpen, i.AcceptedAtUtc);
}
