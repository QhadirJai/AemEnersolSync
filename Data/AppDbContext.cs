using AemEnersolSync.Models;
using Microsoft.EntityFrameworkCore;

namespace AemEnersolSync.Data;

/// <summary>
/// Code-First context: these entities define the schema, which is created via
/// EF Core migrations. Nothing here is scaffolded from an existing database.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Platform> Platforms => Set<Platform>();

    public DbSet<Well> Wells => Set<Well>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Platform>(entity =>
        {
            entity.HasKey(p => p.Id);

            // Ids come from the source API, so SQL Server must not generate them —
            // otherwise inserting a fetched Id would be silently replaced.
            entity.Property(p => p.Id).ValueGeneratedNever();

            entity.Property(p => p.UniqueName).IsRequired().HasMaxLength(200);

            entity.HasMany(p => p.Wells)
                  .WithOne(w => w.Platform!)
                  .HasForeignKey(w => w.PlatformId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Well>(entity =>
        {
            entity.HasKey(w => w.Id);

            entity.Property(w => w.Id).ValueGeneratedNever();

            entity.Property(w => w.UniqueName).IsRequired().HasMaxLength(200);
        });
    }
}
