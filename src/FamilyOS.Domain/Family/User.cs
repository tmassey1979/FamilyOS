using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Family;

/// <summary>
/// FamilyOS User linked to a Keycloak identity.
/// </summary>
public class User : Entity
{
    public string ExternalIdentityId { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public Guid? FamilyId { get; private set; }
    public Guid? FamilyMemberId { get; private set; }

    private User() { }

    public static User Create(string externalIdentityId, string email, string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(externalIdentityId))
            throw new ArgumentException("External identity id is required.", nameof(externalIdentityId));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        return new User
        {
            ExternalIdentityId = externalIdentityId,
            Email = email.Trim().ToLowerInvariant(),
            FirstName = firstName?.Trim() ?? string.Empty,
            LastName = lastName?.Trim() ?? string.Empty
        };
    }

    public void AssignToFamily(Guid familyId, Guid familyMemberId)
    {
        if (FamilyId.HasValue && FamilyId != familyId)
            throw new InvalidOperationException("User already belongs to a different family.");

        FamilyId = familyId;
        FamilyMemberId = familyMemberId;
        MarkUpdated();
    }

    public string DisplayName => string.IsNullOrWhiteSpace($"{FirstName} {LastName}".Trim())
        ? Email
        : $"{FirstName} {LastName}".Trim();
}
