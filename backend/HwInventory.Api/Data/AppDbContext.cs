using System.Text.Json;
using HwInventory.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HwInventory.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Hardware> Hardware => Set<Hardware>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<HardwareProject> HardwareProjects => Set<HardwareProject>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<HardwareConfig> HardwareConfigs => Set<HardwareConfig>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ---------- JSON ----------
        var jsonConverter = new ValueConverter<JsonDocument?, string?>(
            v => v == null ? null : v.RootElement.GetRawText(),
            v => string.IsNullOrEmpty(v) ? null : JsonDocument.Parse(v, default));
        var jsonComparer = new ValueComparer<JsonDocument?>(
            (a, c) => (a == null && c == null) || (a != null && c != null && a.RootElement.GetRawText() == c.RootElement.GetRawText()),
            v => v == null ? 0 : v.RootElement.GetRawText().GetHashCode(),
            v => v == null ? null : JsonDocument.Parse(v.RootElement.GetRawText(), default));

        // ---------- Apply SQLite-compatible DateTimeOffset converter ----------
        // SQLite stores DateTimeOffset as TEXT but the EF Core 9 provider cannot translate
        // comparison expressions (>=, <) on those columns. Storing as binary (long ticks)
        // keeps round-trip values stable AND makes comparisons translatable. On Postgres we
        // keep the native timestamptz mapping.
        if (Database.IsSqlite())
        {
            var dtoBinary = new DateTimeOffsetToBinaryConverter();
            foreach (var entity in b.Model.GetEntityTypes())
            {
                foreach (var prop in entity.GetProperties())
                {
                    if (prop.ClrType == typeof(DateTimeOffset) || prop.ClrType == typeof(DateTimeOffset?))
                    {
                        prop.SetValueConverter(dtoBinary);
                    }
                }
            }
        }

        // ---------- Category ----------
        b.Entity<Category>(e =>
        {
            e.HasIndex(c => c.Slug).IsUnique();
            e.Property(c => c.Name).IsRequired().HasMaxLength(120);
            e.Property(c => c.Slug).IsRequired().HasMaxLength(120);
            e.HasOne(c => c.Parent).WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Tag ----------
        b.Entity<Tag>(e =>
        {
            e.HasIndex(t => t.Name).IsUnique();
            e.Property(t => t.Name).IsRequired().HasMaxLength(120);
        });

        // ---------- Hardware ----------
        b.Entity<Hardware>(e =>
        {
            e.Property(h => h.Name).IsRequired().HasMaxLength(200);
            e.Property(h => h.Condition).HasConversion<string>().HasMaxLength(20);
            e.Property(h => h.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(h => h.Identifiers).HasConversion(jsonConverter).Metadata.SetValueComparer(jsonComparer);
            e.Property(h => h.Specs).HasConversion(jsonConverter).Metadata.SetValueComparer(jsonComparer);
            e.Property(h => h.Links).HasConversion(jsonConverter).Metadata.SetValueComparer(jsonComparer);

            e.HasIndex(h => h.LastUsedAt);
            e.HasIndex(h => h.LastActivityAt);
            e.HasIndex(h => h.ArchivedAt);
            e.HasIndex(h => h.Status);

            e.HasQueryFilter(h => h.ArchivedAt == null);

            // M:N skip-navigation: Hardware <-> Category.
            // The composite PK (HardwareId, CategoryId) already enforces idempotency
            // of link operations, so no separate UNIQUE index is needed.
            e.HasMany(h => h.Categories)
             .WithMany(c => c.Hardware)
             .UsingEntity(j => j.ToTable("hardware_category"));

            // M:N skip-navigation: Hardware <-> Tag.
            e.HasMany(h => h.Tags)
             .WithMany(t => t.Hardware)
             .UsingEntity(j => j.ToTable("hardware_tag"));
        });

        // ---------- Project ----------
        b.Entity<Project>(e =>
        {
            e.HasIndex(p => p.Slug).IsUnique();
            e.Property(p => p.Title).IsRequired().HasMaxLength(200);
            e.Property(p => p.Slug).IsRequired().HasMaxLength(200);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Priority).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Links).HasConversion(jsonConverter).Metadata.SetValueComparer(jsonComparer);

            e.HasIndex(p => p.ArchivedAt);
            e.HasIndex(p => p.Status);

            e.HasQueryFilter(p => p.ArchivedAt == null);
        });

        // ---------- HardwareProject (explicit join) ----------
        b.Entity<HardwareProject>(e =>
        {
            // Composite PK already guarantees uniqueness of (hardware_id, project_id),
            // so the link-or-update operation is naturally idempotent.
            e.HasKey(hp => new { hp.HardwareId, hp.ProjectId });
            e.HasOne(hp => hp.Hardware).WithMany(h => h.HardwareProjects).HasForeignKey(hp => hp.HardwareId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(hp => hp.Project).WithMany(p => p.HardwareProjects).HasForeignKey(hp => hp.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.Property(hp => hp.Role).HasMaxLength(60);
        });

        // ---------- Activity ----------
        b.Entity<Activity>(e =>
        {
            e.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Metadata).HasConversion(jsonConverter).Metadata.SetValueComparer(jsonComparer);
            e.HasIndex(a => a.OccurredAt);
            e.HasIndex(a => new { a.HardwareId, a.OccurredAt });
            e.HasOne(a => a.Hardware).WithMany(h => h.Activities).HasForeignKey(a => a.HardwareId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Project).WithMany(p => p.Activities).HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.SetNull);
        });

        // ---------- HardwareConfig ----------
        b.Entity<HardwareConfig>(e =>
        {
            e.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Name).IsRequired().HasMaxLength(200);
            e.HasIndex(c => new { c.HardwareId, c.Kind, c.IsCurrent });
            e.HasOne(c => c.Hardware).WithMany(h => h.Configs).HasForeignKey(c => c.HardwareId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Activity).WithMany().HasForeignKey(c => c.ActivityId).OnDelete(DeleteBehavior.SetNull);
        });

        // ---------- Loan ----------
        b.Entity<Loan>(e =>
        {
            e.Property(l => l.LoanedTo).IsRequired().HasMaxLength(200);
            e.HasIndex(l => l.ReturnedAt);
            e.HasIndex(l => l.DueAt);
            e.HasOne(l => l.Hardware).WithMany(h => h.Loans).HasForeignKey(l => l.HardwareId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Attachment ----------
        b.Entity<Attachment>(e =>
        {
            e.Property(a => a.StorageBackend).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.StorageKey).IsRequired().HasMaxLength(500);
            e.Property(a => a.OriginalFilename).IsRequired().HasMaxLength(500);
            e.HasIndex(a => a.HardwareId);
            e.HasIndex(a => a.ProjectId);
            e.HasIndex(a => a.ActivityId);
            e.HasOne(a => a.Hardware).WithMany().HasForeignKey(a => a.HardwareId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Project).WithMany().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Activity).WithMany().HasForeignKey(a => a.ActivityId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override int SaveChanges()
    {
        StampTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Modified)
            {
                var prop = entry.Metadata.FindProperty("UpdatedAt");
                if (prop != null)
                {
                    entry.Property("UpdatedAt").CurrentValue = now;
                }
            }
        }
    }
}
