using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Family;

public class Family : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string? TimeZone { get; private set; }
    public string? Currency { get; private set; }

    private readonly List<FamilyMember> _members = new();
    public IReadOnlyCollection<FamilyMember> Members => _members.AsReadOnly();

    private readonly List<DeclineReason> _declineReasons = new();
    public IReadOnlyCollection<DeclineReason> DeclineReasons => _declineReasons.AsReadOnly();

    private Family() { }

    public static Family Create(string name, string? timeZone = "America/Chicago", string? currency = "USD")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Family name is required.", nameof(name));

        var family = new Family
        {
            Name = name.Trim(),
            TimeZone = timeZone,
            Currency = currency
        };

        family.SeedDefaultDeclineReasons();
        return family;
    }

    public FamilyMember AddMember(User user, FamilyRole role, string displayName)
    {
        if (_members.Any(m => m.UserId == user.Id && !m.IsDeleted))
            throw new InvalidOperationException("User is already a member of this family.");

        var member = FamilyMember.Create(this.Id, user.Id, role, displayName);
        _members.Add(member);
        MarkUpdated();
        return member;
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Family name is required.", nameof(name));
        Name = name.Trim();
        MarkUpdated();
    }

    private void SeedDefaultDeclineReasons()
    {
        var defaults = new[]
        {
            ("Can't do it today", 1),
            ("Already committed", 2),
            ("Don't know how", 3),
            ("Not my responsibility", 4),
            ("Other", 5)
        };

        foreach (var (text, order) in defaults)
            _declineReasons.Add(DeclineReason.Create(this.Id, text, order));
    }

    public DeclineReason AddDeclineReason(string text, int sortOrder)
    {
        var reason = DeclineReason.Create(this.Id, text, sortOrder);
        _declineReasons.Add(reason);
        MarkUpdated();
        return reason;
    }
}
