using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

public static class EnsureFamilyInvitesSchema
{
    public static async Task RunAsync(FamilyOsDbContext db, ILogger logger)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS familyos."FamilyInvites" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "FamilyId" uuid NOT NULL,
                    "Code" character varying(16) NOT NULL,
                    "Role" character varying(20) NOT NULL,
                    "EmailHint" character varying(320) NULL,
                    "CreatedByMemberId" uuid NOT NULL,
                    "ExpiresAtUtc" timestamp with time zone NOT NULL,
                    "AcceptedAtUtc" timestamp with time zone NULL,
                    "AcceptedByMemberId" uuid NULL,
                    "IsRevoked" boolean NOT NULL DEFAULT false,
                    "CreatedAtUtc" timestamp with time zone NOT NULL,
                    "UpdatedAtUtc" timestamp with time zone NULL,
                    "IsDeleted" boolean NOT NULL DEFAULT false
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_FamilyInvites_Code"
                    ON familyos."FamilyInvites" ("Code");
                CREATE INDEX IF NOT EXISTS "IX_FamilyInvites_FamilyId_IsRevoked"
                    ON familyos."FamilyInvites" ("FamilyId", "IsRevoked");
                """);
            logger.LogInformation("FamilyInvites schema ensured");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ensure FamilyInvites schema failed");
        }
    }
}
