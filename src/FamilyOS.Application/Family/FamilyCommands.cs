using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Family;

public record CreateFamilyCommand(
    string Name,
    string? TimeZone,
    string? Currency,
    string OwnerDisplayName,
    string OwnerEmail,
    string? OwnerFirstName,
    string? OwnerLastName) : IRequest<FamilyDto>;

public record AddFamilyMemberCommand(
    string Email,
    string DisplayName,
    FamilyRole Role,
    string? ExternalIdentityId,
    string? FirstName,
    string? LastName) : IRequest<MemberDto>;

public record ChangeMemberRoleCommand(Guid MemberId, FamilyRole Role) : IRequest<MemberDto>;
public record DeactivateMemberCommand(Guid MemberId) : IRequest<MemberDto>;

public class FamilyCommandHandlers :
    IRequestHandler<CreateFamilyCommand, FamilyDto>,
    IRequestHandler<AddFamilyMemberCommand, MemberDto>,
    IRequestHandler<ChangeMemberRoleCommand, MemberDto>,
    IRequestHandler<DeactivateMemberCommand, MemberDto>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public FamilyCommandHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<FamilyDto> Handle(CreateFamilyCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);

        // If already bound to a family, reject
        if (_current.FamilyId.HasValue)
            throw new DomainException("You already belong to a family. Leave it before creating another.");

        var externalId = _current.ExternalIdentityId
            ?? throw new ForbiddenException("Sign in before creating a family.");

        // Upsert User for this identity
        var user = await _db.Users.FirstOrDefaultAsync(u => u.ExternalIdentityId == externalId, ct);
        if (user == null)
        {
            user = User.Create(
                externalId,
                string.IsNullOrWhiteSpace(cmd.OwnerEmail) ? $"{externalId}@familyos.local" : cmd.OwnerEmail,
                cmd.OwnerFirstName ?? "Owner",
                cmd.OwnerLastName ?? "");
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
        }
        else if (user.FamilyId.HasValue)
        {
            throw new DomainException("This account is already linked to a family.");
        }

        var family = FamilyOS.Domain.Family.Family.Create(cmd.Name, cmd.TimeZone ?? "America/Chicago", cmd.Currency ?? "USD");
        _db.Families.Add(family);
        await _db.SaveChangesAsync(ct); // get Family.Id

        var displayName = string.IsNullOrWhiteSpace(cmd.OwnerDisplayName)
            ? user.DisplayName
            : cmd.OwnerDisplayName.Trim();

        var owner = family.AddMember(user, FamilyRole.Owner, displayName);
        // Members + DeclineReasons are on family navigations — EF persists with Family
        user.AssignToFamily(family.Id, owner.Id);
        await _db.SaveChangesAsync(ct);

        return new FamilyDto(
            family.Id, family.Name, family.TimeZone, family.Currency,
            new List<MemberDto>
            {
                new(owner.Id, user.Id, owner.DisplayName, owner.Role, owner.IsActive)
            });
    }

    public async Task<MemberDto> Handle(AddFamilyMemberCommand cmd, CancellationToken ct)
    {
        var (familyId, _, role) = await _current.RequireFamilyAsync(ct);
        if (role is not (FamilyRole.Owner or FamilyRole.Adult))
            throw new ForbiddenException("Only Owner or Adult can add members.");

        var family = await _db.Families
            .Include(f => f.Members)
            .FirstOrDefaultAsync(f => f.Id == familyId, ct)
            ?? throw new NotFoundException("Family", familyId);

        var externalId = string.IsNullOrWhiteSpace(cmd.ExternalIdentityId)
            ? cmd.Email.Trim().ToLowerInvariant()
            : cmd.ExternalIdentityId.Trim();

        var existingUser = await _db.Users.FirstOrDefaultAsync(
            u => u.ExternalIdentityId == externalId || u.Email == cmd.Email.Trim().ToLowerInvariant(), ct);

        User user;
        if (existingUser != null)
        {
            if (existingUser.FamilyId.HasValue && existingUser.FamilyId != familyId)
                throw new DomainException("User already belongs to a different family.");
            user = existingUser;
        }
        else
        {
            user = User.Create(
                externalId,
                cmd.Email,
                cmd.FirstName ?? cmd.DisplayName.Split(' ').FirstOrDefault() ?? "Member",
                cmd.LastName ?? "");
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
        }

        if (family.Members.Any(m => m.UserId == user.Id && m.IsActive))
            throw new DomainException("User is already an active member of this family.");

        var member = family.AddMember(user, cmd.Role, cmd.DisplayName);
        user.AssignToFamily(family.Id, member.Id);
        await _db.SaveChangesAsync(ct);

        return new MemberDto(member.Id, user.Id, member.DisplayName, member.Role, member.IsActive);
    }

    public async Task<MemberDto> Handle(ChangeMemberRoleCommand cmd, CancellationToken ct)
    {
        var (familyId, actorMemberId, role) = await _current.RequireFamilyAsync(ct);
        if (role != FamilyRole.Owner)
            throw new ForbiddenException("Only the Owner can change roles.");

        var member = await _db.FamilyMembers
            .FirstOrDefaultAsync(m => m.Id == cmd.MemberId && m.FamilyId == familyId, ct)
            ?? throw new NotFoundException("Member", cmd.MemberId);

        if (member.Id == actorMemberId && cmd.Role != FamilyRole.Owner)
            throw new DomainException("Owner cannot demote themselves. Transfer ownership first.");

        if (cmd.Role == FamilyRole.Owner && member.Role != FamilyRole.Owner)
        {
            // demote current owner to Adult when promoting another
            var owners = await _db.FamilyMembers
                .Where(m => m.FamilyId == familyId && m.Role == FamilyRole.Owner && m.IsActive)
                .ToListAsync(ct);
            foreach (var o in owners)
                o.ChangeRole(FamilyRole.Adult);
        }

        member.ChangeRole(cmd.Role);
        await _db.SaveChangesAsync(ct);
        return new MemberDto(member.Id, member.UserId, member.DisplayName, member.Role, member.IsActive);
    }

    public async Task<MemberDto> Handle(DeactivateMemberCommand cmd, CancellationToken ct)
    {
        var (familyId, actorMemberId, role) = await _current.RequireFamilyAsync(ct);
        if (role != FamilyRole.Owner)
            throw new ForbiddenException("Only the Owner can deactivate members.");

        var member = await _db.FamilyMembers
            .FirstOrDefaultAsync(m => m.Id == cmd.MemberId && m.FamilyId == familyId, ct)
            ?? throw new NotFoundException("Member", cmd.MemberId);

        if (member.Id == actorMemberId)
            throw new DomainException("Cannot deactivate yourself.");

        if (member.Role == FamilyRole.Owner)
            throw new DomainException("Cannot deactivate the Owner.");

        member.Deactivate();
        await _db.SaveChangesAsync(ct);
        return new MemberDto(member.Id, member.UserId, member.DisplayName, member.Role, member.IsActive);
    }
}
