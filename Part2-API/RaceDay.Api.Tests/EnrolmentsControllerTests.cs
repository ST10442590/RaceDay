using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Models;
using RaceDay.Api.Models.Dtos;
using RaceDay.Api.Security;
using Xunit;

namespace RaceDay.Api.Tests;

// Covers Participants entering events, and the rules that protect those
// entries: one per event, the right category, the right age, and capacity.
public class EnrolmentsControllerTests
{
    [Fact]
    public async Task Enter_WithAValidCategory_RecordsParticipantEventAndCategory()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var participant = TestDatabase.Participant(db);
        var raceEvent = TestDatabase.TestEvent(db);
        var category = raceEvent.Categories.First(c => c.CategoryName == "10km Run");

        var controller = new EnrolmentsController(db);
        TestDatabase.SignIn(controller, participant, RoleNames.Participant);

        var result = await controller.Enter(new CreateEnrolmentRequest
        {
            EventId = raceEvent.EventId,
            CategoryId = category.CategoryId
        });

        Assert.IsType<CreatedAtActionResult>(result.Result);

        var saved = db.Enrolments.Single();
        Assert.Equal(participant.UserId, saved.ParticipantId);
        Assert.Equal(raceEvent.EventId, saved.EventId);
        Assert.Equal(category.CategoryId, saved.CategoryId);
        Assert.Equal(EnrolmentStatuses.Confirmed, saved.Status);
    }

    [Fact]
    public async Task Enter_TwiceIntoTheSameEvent_IsRejected()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var participant = TestDatabase.Participant(db);
        var raceEvent = TestDatabase.TestEvent(db);
        var category = raceEvent.Categories.First(c => c.CategoryName == "10km Run");

        var controller = new EnrolmentsController(db);
        TestDatabase.SignIn(controller, participant, RoleNames.Participant);

        var request = new CreateEnrolmentRequest
        {
            EventId = raceEvent.EventId,
            CategoryId = category.CategoryId
        };

        await controller.Enter(request);
        var secondAttempt = await controller.Enter(request);

        Assert.IsType<ConflictObjectResult>(secondAttempt.Result);
        Assert.Single(db.Enrolments);
    }

    [Fact]
    public async Task Enter_WithACategoryFromADifferentEvent_IsRejected()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var participant = TestDatabase.Participant(db);
        var organiser = TestDatabase.Organiser(db);
        var firstEvent = TestDatabase.TestEvent(db);

        // A second event, with its own category.
        var secondEvent = new Event
        {
            OrganiserId = organiser.UserId,
            EventTypeId = 2,
            Name = "Second Event",
            EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(4)),
            StartTime = new TimeOnly(8, 0),
            DistanceKm = 5m,
            VenueName = "Park",
            City = "Durban",
            Province = "KwaZulu-Natal",
            RegistrationDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
            Status = EventStatuses.Open,
            Categories =
            {
                new EventCategory
                {
                    CategoryName = "5km Walk",
                    CategoryType = CategoryTypes.Distance,
                    DistanceKm = 5m,
                    EntryFee = 50m,
                    MaxParticipants = 100
                }
            }
        };

        db.Events.Add(secondEvent);
        db.SaveChanges();

        var controller = new EnrolmentsController(db);
        TestDatabase.SignIn(controller, participant, RoleNames.Participant);

        // The category belongs to the first event, not the second.
        var result = await controller.Enter(new CreateEnrolmentRequest
        {
            EventId = secondEvent.EventId,
            CategoryId = firstEvent.Categories.First().CategoryId
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.Enrolments);
    }

    [Fact]
    public async Task Enter_WhenTooOldForAnAgeCategory_IsRejected()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var participant = TestDatabase.Participant(db); // born 1995
        var raceEvent = TestDatabase.TestEvent(db);
        var under20 = raceEvent.Categories.First(c => c.CategoryName == "Under 20");

        var controller = new EnrolmentsController(db);
        TestDatabase.SignIn(controller, participant, RoleNames.Participant);

        var result = await controller.Enter(new CreateEnrolmentRequest
        {
            EventId = raceEvent.EventId,
            CategoryId = under20.CategoryId
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.Enrolments);
    }

    [Fact]
    public async Task Enter_WhenTheCategoryIsFull_IsRejected()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var raceEvent = TestDatabase.TestEvent(db);
        var category = raceEvent.Categories.First(c => c.CategoryName == "10km Run");

        // The seeded category caps at 2, so fill both places.
        db.Enrolments.AddRange(
            new Enrolment
            {
                ParticipantId = TestDatabase.Organiser(db).UserId,
                EventId = raceEvent.EventId,
                CategoryId = category.CategoryId,
                Status = EnrolmentStatuses.Confirmed
            },
            new Enrolment
            {
                ParticipantId = TestDatabase.OtherOrganiser(db).UserId,
                EventId = raceEvent.EventId,
                CategoryId = category.CategoryId,
                Status = EnrolmentStatuses.Confirmed
            });
        db.SaveChanges();

        var controller = new EnrolmentsController(db);
        TestDatabase.SignIn(controller, TestDatabase.Participant(db), RoleNames.Participant);

        var result = await controller.Enter(new CreateEnrolmentRequest
        {
            EventId = raceEvent.EventId,
            CategoryId = category.CategoryId
        });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetForEvent_AsTheOwningOrganiser_ListsTheEntries()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var raceEvent = TestDatabase.TestEvent(db);
        var participant = TestDatabase.Participant(db);

        db.Enrolments.Add(new Enrolment
        {
            ParticipantId = participant.UserId,
            EventId = raceEvent.EventId,
            CategoryId = raceEvent.Categories.First().CategoryId,
            RaceNumber = "T001",
            Status = EnrolmentStatuses.Confirmed
        });
        db.SaveChanges();

        var controller = new EnrolmentsController(db);
        TestDatabase.SignIn(controller, TestDatabase.Organiser(db), RoleNames.Organiser);

        var result = await controller.GetForEvent(raceEvent.EventId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var entries = Assert.IsAssignableFrom<IEnumerable<EnrolmentDetailResponse>>(ok.Value);
        Assert.Single(entries);
    }

    [Fact]
    public async Task GetForEvent_AsADifferentOrganiser_IsForbidden()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var raceEvent = TestDatabase.TestEvent(db);

        var controller = new EnrolmentsController(db);
        TestDatabase.SignIn(controller, TestDatabase.OtherOrganiser(db), RoleNames.Organiser);

        var result = await controller.GetForEvent(raceEvent.EventId);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Cancel_SomeoneElsesEnrolment_IsForbidden()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var raceEvent = TestDatabase.TestEvent(db);

        var enrolment = new Enrolment
        {
            ParticipantId = TestDatabase.Participant(db).UserId,
            EventId = raceEvent.EventId,
            CategoryId = raceEvent.Categories.First().CategoryId,
            Status = EnrolmentStatuses.Confirmed
        };
        db.Enrolments.Add(enrolment);
        db.SaveChanges();

        var controller = new EnrolmentsController(db);
        TestDatabase.SignIn(controller, TestDatabase.OtherOrganiser(db), RoleNames.Participant);

        var result = await controller.Cancel(enrolment.EnrolmentId);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Equal(EnrolmentStatuses.Confirmed, db.Enrolments.Single().Status);
    }
}