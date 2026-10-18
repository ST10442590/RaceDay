using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

// Entity Framework Core database context for RaceDay.
// The configuration below reproduces the Part 1 ERD and SQL script exactly:
// the same tables, keys, unique constraints, defaults and check constraints.
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventCategory> EventCategories => Set<EventCategory>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------------------------------------------------------------
        // 1. Roles
        // ---------------------------------------------------------------
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.RoleId);
            entity.Property(e => e.RoleName).HasMaxLength(30).IsRequired();
            entity.HasIndex(e => e.RoleName).IsUnique();
        });

        // ---------------------------------------------------------------
        // 2. Users
        // ---------------------------------------------------------------
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", t => t.HasCheckConstraint(
                "CK_Users_Gender",
                "Gender IS NULL OR Gender IN ('Male', 'Female', 'Other')"));

            entity.HasKey(e => e.UserId);
            entity.Property(e => e.FirstName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.City).HasMaxLength(60);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            // No two accounts may share an email address.
            entity.HasIndex(e => e.Email).IsUnique();

            entity.HasOne(e => e.Role)
                  .WithMany(r => r.Users)
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------------------------------------------------------
        // 3. EventTypes
        // ---------------------------------------------------------------
        modelBuilder.Entity<EventType>(entity =>
        {
            entity.ToTable("EventTypes");
            entity.HasKey(e => e.EventTypeId);
            entity.Property(e => e.TypeName).HasMaxLength(30).IsRequired();
            entity.HasIndex(e => e.TypeName).IsUnique();
        });

        // ---------------------------------------------------------------
        // 4. Events
        // ---------------------------------------------------------------
        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Events", t =>
            {
                t.HasCheckConstraint(
                    "CK_Events_Status",
                    "Status IN ('Upcoming', 'Open', 'Closed', 'Completed', 'Cancelled')");
                t.HasCheckConstraint(
                    "CK_Events_Deadline",
                    "RegistrationDeadline <= EventDate");
                t.HasCheckConstraint(
                    "CK_Events_Distance",
                    "DistanceKm > 0");
            });

            entity.HasKey(e => e.EventId);
            entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.DistanceKm).HasPrecision(6, 2);
            entity.Property(e => e.VenueName).HasMaxLength(120).IsRequired();
            entity.Property(e => e.City).HasMaxLength(60).IsRequired();
            entity.Property(e => e.Province).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Latitude).HasPrecision(9, 6);
            entity.Property(e => e.Longitude).HasPrecision(9, 6);
            entity.Property(e => e.RouteUrl).HasMaxLength(500);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired()
                  .HasDefaultValue(EventStatuses.Upcoming);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(e => e.OrganiserId);
            entity.HasIndex(e => e.EventDate);

            entity.HasOne(e => e.Organiser)
                  .WithMany(u => u.OrganisedEvents)
                  .HasForeignKey(e => e.OrganiserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.EventType)
                  .WithMany(t => t.Events)
                  .HasForeignKey(e => e.EventTypeId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------------------------------------------------------
        // 5. EventCategories
        // ---------------------------------------------------------------
        modelBuilder.Entity<EventCategory>(entity =>
        {
            entity.ToTable("EventCategories", t =>
            {
                t.HasCheckConstraint(
                    "CK_EventCategories_Type",
                    "CategoryType IN ('Age', 'Distance')");
                t.HasCheckConstraint(
                    "CK_EventCategories_Distance",
                    "(CategoryType = 'Distance' AND DistanceKm IS NOT NULL AND DistanceKm > 0) " +
                    "OR (CategoryType = 'Age' AND DistanceKm IS NULL)");
                t.HasCheckConstraint(
                    "CK_EventCategories_Age",
                    "MinAge >= 0 AND (MaxAge IS NULL OR MaxAge >= MinAge)");
                t.HasCheckConstraint("CK_EventCategories_Fee", "EntryFee >= 0");
                t.HasCheckConstraint("CK_EventCategories_Max", "MaxParticipants > 0");
            });

            entity.HasKey(e => e.CategoryId);
            entity.Property(e => e.CategoryName).HasMaxLength(80).IsRequired();
            entity.Property(e => e.CategoryType).HasMaxLength(10).IsRequired();
            entity.Property(e => e.DistanceKm).HasPrecision(6, 2);
            entity.Property(e => e.MinAge).HasDefaultValue(0);
            entity.Property(e => e.EntryFee).HasPrecision(8, 2).HasDefaultValue(0m);

            // A category name may appear only once per event.
            entity.HasIndex(e => new { e.EventId, e.CategoryName }).IsUnique();
            entity.HasIndex(e => e.EventId);

            // Alternate key so an Enrolment can reference (CategoryId, EventId)
            // together, which guarantees the category belongs to the event.
            entity.HasAlternateKey(e => new { e.CategoryId, e.EventId });

            entity.HasOne(e => e.Event)
                  .WithMany(ev => ev.Categories)
                  .HasForeignKey(e => e.EventId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------------------------------------------------------
        // 6. Enrolments
        // ---------------------------------------------------------------
        modelBuilder.Entity<Enrolment>(entity =>
        {
            entity.ToTable("Enrolments", t => t.HasCheckConstraint(
                "CK_Enrolments_Status",
                "Status IN ('Pending', 'Confirmed', 'Cancelled')"));

            entity.HasKey(e => e.EnrolmentId);
            entity.Property(e => e.RaceNumber).HasMaxLength(10);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired()
                  .HasDefaultValue(EnrolmentStatuses.Confirmed);
            entity.Property(e => e.EnrolledAt).HasDefaultValueSql("SYSUTCDATETIME()");

            // A participant may enter each event only once.
            entity.HasIndex(e => new { e.ParticipantId, e.EventId }).IsUnique();
            entity.HasIndex(e => e.EventId);

            entity.HasOne(e => e.Participant)
                  .WithMany(u => u.Enrolments)
                  .HasForeignKey(e => e.ParticipantId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Composite foreign key: the chosen category must belong to the
            // chosen event, enforced by the database rather than by code.
            // There is deliberately no second foreign key straight to Events:
            // EventId reaches Events through this key, and a second cascade
            // path would be rejected by SQL Server.
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Enrolments)
                  .HasForeignKey(e => new { e.CategoryId, e.EventId })
                  .HasPrincipalKey(c => new { c.CategoryId, c.EventId })
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------------------------------------------------------
        // 7. Results
        // ---------------------------------------------------------------
        modelBuilder.Entity<Result>(entity =>
        {
            entity.ToTable("Results", t =>
            {
                t.HasCheckConstraint(
                    "CK_Results_Status",
                    "ResultStatus IN ('Finished', 'DNF', 'DNS', 'DQ')");
                t.HasCheckConstraint(
                    "CK_Results_TimeAndPosition",
                    "(ResultStatus = 'Finished' AND FinishTime IS NOT NULL " +
                    "AND FinishPosition IS NOT NULL AND FinishPosition > 0) " +
                    "OR (ResultStatus <> 'Finished' AND FinishTime IS NULL " +
                    "AND FinishPosition IS NULL)");
            });

            entity.HasKey(e => e.ResultId);
            entity.Property(e => e.ResultStatus).HasMaxLength(10).IsRequired()
                  .HasDefaultValue(ResultStatuses.Finished);
            entity.Property(e => e.RecordedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            // One result per enrolment, at most.
            entity.HasIndex(e => e.EnrolmentId).IsUnique();

            entity.HasOne(e => e.Enrolment)
                  .WithOne(en => en.Result)
                  .HasForeignKey<Result>(e => e.EnrolmentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.RecordedBy)
                  .WithMany(u => u.RecordedResults)
                  .HasForeignKey(e => e.RecordedById)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------------------------------------------------------
        // Seed the two lookup tables. Demo users and events are seeded at
        // startup instead, because passwords must be hashed at runtime.
        // ---------------------------------------------------------------
        modelBuilder.Entity<Role>().HasData(
            new Role { RoleId = RoleNames.OrganiserId, RoleName = RoleNames.Organiser },
            new Role { RoleId = RoleNames.ParticipantId, RoleName = RoleNames.Participant });

        modelBuilder.Entity<EventType>().HasData(
            new EventType { EventTypeId = 1, TypeName = "Run" },
            new EventType { EventTypeId = 2, TypeName = "Walk" },
            new EventType { EventTypeId = 3, TypeName = "Cycle" });
    }
}