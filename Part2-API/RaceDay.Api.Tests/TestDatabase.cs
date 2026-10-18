using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Tests;

// Builds a throwaway database and a fake session for each test, so tests
// never touch SQL Server and never affect one another.
public static class TestDatabase
{
    public const string Password = "TestPass#2026";

    public static RaceDayDbContext Create()
    {
        // A unique name per call gives every test its own isolated database.
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase($"RaceDayTests-{Guid.NewGuid()}")
            .Options;

        var db = new RaceDayDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    // Adds the two roles, the three event types, one Organiser, one
    // Participant, and one event with a distance and an age category.
    public static void Seed(RaceDayDbContext db)
    {
        if (!db.Roles.Any())
        {
            db.Roles.AddRange(
                new Role { RoleId = RoleNames.OrganiserId, RoleName = RoleNames.Organiser },
                new Role { RoleId = RoleNames.ParticipantId, RoleName = RoleNames.Participant });
        }

        if (!db.EventTypes.Any())
        {
            db.EventTypes.AddRange(
                new EventType { EventTypeId = 1, TypeName = "Run" },
                new EventType { EventTypeId = 2, TypeName = "Walk" },
                new EventType { EventTypeId = 3, TypeName = "Cycle" });
        }

        db.SaveChanges();

        var hasher = new PasswordHasher<User>();

        var organiser = new User
        {
            RoleId = RoleNames.OrganiserId,
            FirstName = "Olwethu",
            LastName = "Organiser",
            Email = "organiser@test.local",
            DateOfBirth = new DateOnly(1980, 5, 5)
        };
        organiser.PasswordHash = hasher.HashPassword(organiser, Password);

        var otherOrganiser = new User
        {
            RoleId = RoleNames.OrganiserId,
            FirstName = "Rival",
            LastName = "Organiser",
            Email = "rival@test.local",
            DateOfBirth = new DateOnly(1982, 6, 6)
        };
        otherOrganiser.PasswordHash = hasher.HashPassword(otherOrganiser, Password);

        var participant = new User
        {
            RoleId = RoleNames.ParticipantId,
            FirstName = "Palesa",
            LastName = "Participant",
            Email = "participant@test.local",
            DateOfBirth = new DateOnly(1995, 4, 4)
        };
        participant.PasswordHash = hasher.HashPassword(participant, Password);

        db.Users.AddRange(organiser, otherOrganiser, participant);
        db.SaveChanges();

        var raceEvent = new Event
        {
            OrganiserId = organiser.UserId,
            EventTypeId = 1,
            Name = "Test Road Race",
            EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)),
            StartTime = new TimeOnly(7, 0),
            DistanceKm = 10m,
            VenueName = "Test Venue",
            City = "Johannesburg",
            Province = "Gauteng",
            RegistrationDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Status = EventStatuses.Open,
            Categories =
            {
                new EventCategory
                {
                    CategoryName = "10km Run",
                    CategoryType = CategoryTypes.Distance,
                    DistanceKm = 10m,
                    MinAge = 12,
                    EntryFee = 150m,
                    MaxParticipants = 2
                },
                new EventCategory
                {
                    CategoryName = "Under 20",
                    CategoryType = CategoryTypes.Age,
                    MinAge = 14,
                    MaxAge = 19,
                    EntryFee = 120m,
                    MaxParticipants = 50
                }
            }
        };

        db.Events.Add(raceEvent);
        db.SaveChanges();
    }

    public static User Organiser(RaceDayDbContext db) =>
        db.Users.First(u => u.Email == "organiser@test.local");

    public static User OtherOrganiser(RaceDayDbContext db) =>
        db.Users.First(u => u.Email == "rival@test.local");

    public static User Participant(RaceDayDbContext db) =>
        db.Users.First(u => u.Email == "participant@test.local");

    public static Event TestEvent(RaceDayDbContext db) =>
        db.Events.Include(e => e.Categories).First();

    // Attaches a fake session to a controller, standing in for a logged-in
    // user. Passing null leaves nobody logged in.
    public static void SignIn(ControllerBase controller, User? user, string? role)
    {
        var httpContext = new DefaultHttpContext
        {
            Session = new FakeSession()
        };

        if (user is not null && role is not null)
        {
            httpContext.Session.SignIn(user, role);
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }
}

// A minimal in-memory ISession. The real one needs a web server; this holds
// the same values in a dictionary so controllers behave identically.
public class FakeSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();

    public bool IsAvailable => true;
    public string Id => "test-session";
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;
    public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
}