using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

public static class EnsureMealSchema
{
    public static async Task RunAsync(FamilyOsDbContext db, ILogger logger)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS familyos."MealPlans" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "FamilyId" uuid NOT NULL,
                    "WeekStartUtc" timestamp with time zone NOT NULL,
                    "Notes" character varying(1000) NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL,
                    "UpdatedAtUtc" timestamp with time zone NULL,
                    "IsDeleted" boolean NOT NULL DEFAULT false
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_MealPlans_FamilyId_WeekStartUtc"
                    ON familyos."MealPlans" ("FamilyId", "WeekStartUtc");
                CREATE TABLE IF NOT EXISTS familyos."MealEntries" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "MealPlanId" uuid NOT NULL,
                    "FamilyId" uuid NOT NULL,
                    "DayUtc" timestamp with time zone NOT NULL,
                    "Slot" character varying(20) NOT NULL,
                    "DishName" character varying(300) NOT NULL,
                    "Notes" character varying(1000) NULL,
                    "IngredientsCsv" character varying(2000) NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL,
                    "UpdatedAtUtc" timestamp with time zone NULL,
                    "IsDeleted" boolean NOT NULL DEFAULT false
                );
                CREATE INDEX IF NOT EXISTS "IX_MealEntries_MealPlanId_DayUtc_Slot"
                    ON familyos."MealEntries" ("MealPlanId", "DayUtc", "Slot");
                CREATE INDEX IF NOT EXISTS "IX_MealEntries_FamilyId"
                    ON familyos."MealEntries" ("FamilyId");
                """);
            logger.LogInformation("Meal schema ensured");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EnsureMealSchema failed");
        }
    }
}
