using FamilyOS.Domain.Common;

namespace FamilyOS.Application.Interfaces;

public interface ICurrentUserService
{
    string? ExternalIdentityId { get; }
    Guid? UserId { get; }
    Guid? FamilyId { get; }
    Guid? MemberId { get; }
    FamilyRole? Role { get; }
    bool IsAuthenticated { get; }
    Task EnsureLoadedAsync(CancellationToken ct = default);
}
