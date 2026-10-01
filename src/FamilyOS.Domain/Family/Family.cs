using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Family;

public class Family : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string? TimeZoneId { get; private set; }

    private readonly List<FamilyMember> _members = new();
    public IReadOnlyCollection<FamilyMember> Members => _members.AsReadOnly();

    private Family() { }

    public static Family Create(string name, string? timeZoneId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Family name is required.", nameof(name));

        return new Family
        {
            Name = name.Trim(),
            TimeZoneId = timeZoneId
        };
    }

    public FamilyMember AddMember(string displayName, FamilyRole role, Guid? userId = null)
    {
        var member = FamilyMember.Create(Id, displayName, role, userId);
        _members.Add(member);
        MarkUpdated();
        return member;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Family name is required.", nameof(name));
        Name = name.Trim();
        MarkUpdated();
    }
}
