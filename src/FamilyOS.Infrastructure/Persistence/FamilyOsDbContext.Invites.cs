using FamilyOS.Domain.Family;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Infrastructure.Persistence;

public partial class FamilyOsDbContext
{
    public DbSet<FamilyInvite> FamilyInvites => Set<FamilyInvite>();

    private static void ConfigureInvites(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FamilyInvite>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => new { x.FamilyId, x.IsRevoked });
            e.Property(x => x.Code).HasMaxLength(16).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.EmailHint).HasMaxLength(320);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }
}
