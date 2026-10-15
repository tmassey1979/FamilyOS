using System.Security.Cryptography;
using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Family;

/// <summary>Shareable invite for a household (code or deep link).</summary>
public class FamilyInvite : Entity
{
    public Guid FamilyId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public FamilyRole Role { get; private set; } = FamilyRole.Adult;
    public string? EmailHint { get; private set; }
    public Guid CreatedByMemberId { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public Guid? AcceptedByMemberId { get; private set; }
    public bool IsRevoked { get; private set; }

    private FamilyInvite() { }

    public static FamilyInvite Create(
        Guid familyId,
        Guid createdByMemberId,
        FamilyRole role,
        string? emailHint = null,
        int validDays = 14)
    {
        if (role == FamilyRole.Owner)
            throw new ArgumentException("Cannot invite as Owner. Transfer ownership instead.");

        return new FamilyInvite
        {
            FamilyId = familyId,
            Code = GenerateCode(),
            Role = role,
            EmailHint = string.IsNullOrWhiteSpace(emailHint) ? null : emailHint.Trim().ToLowerInvariant(),
            CreatedByMemberId = createdByMemberId,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(Math.Clamp(validDays, 1, 90))
        };
    }

    public bool IsOpen =>
        !IsRevoked && !AcceptedAtUtc.HasValue && ExpiresAtUtc > DateTime.UtcNow;

    public void Accept(Guid memberId)
    {
        if (!IsOpen)
            throw new InvalidOperationException("Invite is no longer valid.");
        AcceptedAtUtc = DateTime.UtcNow;
        AcceptedByMemberId = memberId;
        MarkUpdated();
    }

    public void Revoke()
    {
        if (AcceptedAtUtc.HasValue) return;
        IsRevoked = true;
        MarkUpdated();
    }

    private static string GenerateCode()
    {
        // 8-char uppercase alphanumeric without ambiguous chars
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[8];
        for (var i = 0; i < 8; i++)
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }
}
