using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

// Creates the database from the migrations and seeds demo data the first
// time the API runs. The seed data mirrors the Part 1 SQL script, except
// that passwords are hashed here at runtime rather than stored as literals.
public static class DbInitializer
{
    // Password for every seeded demo account.
    public const string DemoPassword = "RaceDay#2026";

    public static async Task InitialiseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();

        await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync())
        {
            return; // Already seeded.
        }

        var hasher = new PasswordHasher<User>();
        string Hash(User user) => hasher.HashPassword(user, DemoPassword);

        var organisers = new List<User>
        {
            new() { RoleId = RoleNames.OrganiserId, FirstName = "Lerato", LastName = "Mokoena",
                    Email = "lerato.mokoena@raceday.co.za", PhoneNumber = "0821234567",
                    DateOfBirth = new DateOnly(1985, 3, 14), Gender = "Female", City = "Centurion" },
            new() { RoleId = RoleNames.OrganiserId, FirstName = "Johan", LastName = "van Wyk",
                    Email = "johan.vanwyk@raceday.co.za", PhoneNumber = "0837654321",
                    DateOfBirth = new DateOnly(1979, 11, 2), Gender = "Male", City = "Durban" }
        };

        var participants = new List<User>
        {
            new() { RoleId = RoleNames.ParticipantId, FirstName = "Sipho", LastName = "Dlamini",
                    Email = "sipho.dlamini@gmail.com", PhoneNumber = "0712345678",
                    DateOfBirth = new DateOnly(1994, 7, 21), Gender = "Male", City = "Soweto" },
            new() { RoleId = RoleNames.ParticipantId, FirstName = "Aisha", LastName = "Patel",
                    Email = "aisha.patel@outlook.com", PhoneNumber = "0798765432",
                    DateOfBirth = new DateOnly(1998, 1, 9), Gender = "Female", City = "Pretoria" },
            new() { RoleId = RoleNames.ParticipantId, FirstName = "Thandi", LastName = "Nkosi",
                    Email = "thandi.nkosi@gmail.com", PhoneNumber = "0765550123",
                    DateOfBirth = new DateOnly(1990, 9, 30), Gender = "Female", City = "Pietermaritzburg" }
        };

        foreach (var user in organisers.Concat(participants))
        {
            user.PasswordHash = Hash(user);
        }

        db.Users.AddRange(organisers);
        db.Users.AddRange(participants);
        await db.SaveChangesAsync();

        var sunriseRace = new Event
        {
            OrganiserId = organisers[0].UserId,
            EventTypeId = 1, // Run
            Name = "Centurion Sunrise Road Race",
            Description = "Fast, flat road race around Centurion Lake with 10km and 21.1km options.",
            EventDate = new DateOnly(2026, 8, 16),
            StartTime = new TimeOnly(6, 0),
            DistanceKm = 21.10m,
            VenueName = "Centurion Lake Precinct",
            City = "Centurion",
            Province = "Gauteng",
            Latitude = -25.855200m,
            Longitude = 28.189800m,
            RegistrationDeadline = new DateOnly(2026, 8, 9),
            Status = EventStatuses.Completed,
            Categories =
            {
                new EventCategory { CategoryName = "10km Run", CategoryType = CategoryTypes.Distance,
                                    DistanceKm = 10.00m, MinAge = 12, EntryFee = 180.00m,
                                    MaxParticipants = 500, StartTime = new TimeOnly(6, 30) },
                new EventCategory { CategoryName = "21.1km Half Marathon", CategoryType = CategoryTypes.Distance,
                                    DistanceKm = 21.10m, MinAge = 16, EntryFee = 280.00m,
                                    MaxParticipants = 300, StartTime = new TimeOnly(6, 0) }
            }
        };

        var cycleClassic = new Event
        {
            OrganiserId = organisers[1].UserId,
            EventTypeId = 3, // Cycle
            Name = "Valley of a Thousand Hills Cycle Classic",
            Description = "Scenic 100km road cycling classic through the KZN hills, with age categories.",
            EventDate = new DateOnly(2026, 10, 18),
            StartTime = new TimeOnly(6, 30),
            DistanceKm = 100.00m,
            VenueName = "Hillcrest Park",
            City = "Hillcrest",
            Province = "KwaZulu-Natal",
            Latitude = -29.781900m,
            Longitude = 30.763800m,
            RegistrationDeadline = new DateOnly(2026, 10, 11),
            Status = EventStatuses.Open,
            Categories =
            {
                new EventCategory { CategoryName = "Under 20", CategoryType = CategoryTypes.Age,
                                    MinAge = 14, MaxAge = 19, EntryFee = 350.00m,
                                    MaxParticipants = 150, StartTime = new TimeOnly(6, 30) },
                new EventCategory { CategoryName = "Senior", CategoryType = CategoryTypes.Age,
                                    MinAge = 20, MaxAge = 39, EntryFee = 550.00m,
                                    MaxParticipants = 250, StartTime = new TimeOnly(6, 30) },
                new EventCategory { CategoryName = "Veteran 40+", CategoryType = CategoryTypes.Age,
                                    MinAge = 40, EntryFee = 550.00m,
                                    MaxParticipants = 150, StartTime = new TimeOnly(6, 30) }
            }
        };

        var heritageWalk = new Event
        {
            OrganiserId = organisers[0].UserId,
            EventTypeId = 2, // Walk
            Name = "Soweto Heritage Charity Walk",
            Description = "Family-friendly walk past Vilakazi Street in support of local schools.",
            EventDate = new DateOnly(2026, 11, 22),
            StartTime = new TimeOnly(7, 0),
            DistanceKm = 10.00m,
            VenueName = "Orlando Stadium",
            City = "Soweto",
            Province = "Gauteng",
            Latitude = -26.234600m,
            Longitude = 27.906600m,
            RegistrationDeadline = new DateOnly(2026, 11, 15),
            Status = EventStatuses.Upcoming,
            Categories =
            {
                new EventCategory { CategoryName = "5km Family Walk", CategoryType = CategoryTypes.Distance,
                                    DistanceKm = 5.00m, MinAge = 0, EntryFee = 60.00m,
                                    MaxParticipants = 800, StartTime = new TimeOnly(7, 30) },
                new EventCategory { CategoryName = "10km Walk", CategoryType = CategoryTypes.Distance,
                                    DistanceKm = 10.00m, MinAge = 12, EntryFee = 90.00m,
                                    MaxParticipants = 400, StartTime = new TimeOnly(7, 0) }
            }
        };

        db.Events.AddRange(sunriseRace, cycleClassic, heritageWalk);
        await db.SaveChangesAsync();

        var halfMarathon = sunriseRace.Categories.First(c => c.CategoryName == "21.1km Half Marathon");
        var tenKmRun = sunriseRace.Categories.First(c => c.CategoryName == "10km Run");
        var senior = cycleClassic.Categories.First(c => c.CategoryName == "Senior");
        var familyWalk = heritageWalk.Categories.First(c => c.CategoryName == "5km Family Walk");

        var enrolments = new List<Enrolment>
        {
            new() { ParticipantId = participants[0].UserId, EventId = sunriseRace.EventId,
                    CategoryId = halfMarathon.CategoryId, RaceNumber = "H101" },
            new() { ParticipantId = participants[1].UserId, EventId = sunriseRace.EventId,
                    CategoryId = halfMarathon.CategoryId, RaceNumber = "H102" },
            new() { ParticipantId = participants[2].UserId, EventId = sunriseRace.EventId,
                    CategoryId = tenKmRun.CategoryId, RaceNumber = "T201" },
            new() { ParticipantId = participants[0].UserId, EventId = cycleClassic.EventId,
                    CategoryId = senior.CategoryId, RaceNumber = "C301" },
            new() { ParticipantId = participants[1].UserId, EventId = cycleClassic.EventId,
                    CategoryId = senior.CategoryId, RaceNumber = "C302" },
            new() { ParticipantId = participants[2].UserId, EventId = heritageWalk.EventId,
                    CategoryId = familyWalk.CategoryId, Status = EnrolmentStatuses.Pending }
        };

        db.Enrolments.AddRange(enrolments);
        await db.SaveChangesAsync();

        // Results for the completed event, captured by its organiser.
        db.Results.AddRange(
            new Result
            {
                EnrolmentId = enrolments[0].EnrolmentId,
                FinishTime = new TimeOnly(1, 38, 42),
                FinishPosition = 1,
                ResultStatus = ResultStatuses.Finished,
                RecordedById = organisers[0].UserId
            },
            new Result
            {
                EnrolmentId = enrolments[1].EnrolmentId,
                ResultStatus = ResultStatuses.DidNotFinish,
                RecordedById = organisers[0].UserId
            },
            new Result
            {
                EnrolmentId = enrolments[2].EnrolmentId,
                FinishTime = new TimeOnly(0, 54, 5),
                FinishPosition = 1,
                ResultStatus = ResultStatuses.Finished,
                RecordedById = organisers[0].UserId
            });

        await db.SaveChangesAsync();
    }
}
