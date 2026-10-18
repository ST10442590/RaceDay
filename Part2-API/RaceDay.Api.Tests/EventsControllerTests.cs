using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Controllers;
using RaceDay.Api.Models;
using RaceDay.Api.Models.Dtos;
using RaceDay.Api.Security;
using Xunit;

namespace RaceDay.Api.Tests;

// Covers event management by Organisers, including the rule that an
// Organiser may only change events they created.
public class EventsControllerTests
{
    private static CreateEventRequest ValidEvent() => new()
    {
        Name = "Spring Half Marathon",
        EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
        StartTime = new TimeOnly(6, 30),
        DistanceKm = 21.1m,
        EventTypeId = 1,
        VenueName = "City Hall",
        City = "Pretoria",
        Province = "Gauteng",
        RegistrationDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2))
    };

    [Fact]
    public async Task CreateEvent_AsOrganiser_SavesTheEventAgainstThatOrganiser()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var organiser = TestDatabase.Organiser(db);

        var controller = new EventsController(db);
        TestDatabase.SignIn(controller, organiser, RoleNames.Organiser);

        var result = await controller.CreateEvent(ValidEvent());

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var detail = Assert.IsType<EventDetailResponse>(created.Value);

        Assert.Equal(organiser.UserId, detail.OrganiserId);
        Assert.Equal("Spring Half Marathon", detail.Name);
    }

    [Fact]
    public async Task CreateEvent_WithDeadlineAfterTheEventDate_IsRejected()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);

        var controller = new EventsController(db);
        TestDatabase.SignIn(controller, TestDatabase.Organiser(db), RoleNames.Organiser);

        var request = ValidEvent();
        request.RegistrationDeadline = request.EventDate.AddDays(1);

        var result = await controller.CreateEvent(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateEvent_WithUnknownEventType_IsRejected()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);

        var controller = new EventsController(db);
        TestDatabase.SignIn(controller, TestDatabase.Organiser(db), RoleNames.Organiser);

        var request = ValidEvent();
        request.EventTypeId = 99;

        var result = await controller.CreateEvent(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetMyEvents_ReturnsOnlyTheLoggedInOrganisersEvents()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);

        var controller = new EventsController(db);
        TestDatabase.SignIn(controller, TestDatabase.OtherOrganiser(db), RoleNames.Organiser);

        var result = await controller.GetMyEvents();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var events = Assert.IsAssignableFrom<IEnumerable<EventSummaryResponse>>(ok.Value);

        // The seeded event belongs to the other Organiser, so this list is empty.
        Assert.Empty(events);
    }

    [Fact]
    public async Task UpdateEvent_OnAnotherOrganisersEvent_IsForbidden()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var raceEvent = TestDatabase.TestEvent(db);

        var controller = new EventsController(db);
        TestDatabase.SignIn(controller, TestDatabase.OtherOrganiser(db), RoleNames.Organiser);

        var request = new UpdateEventRequest
        {
            Name = "Hijacked Event",
            EventDate = raceEvent.EventDate,
            StartTime = raceEvent.StartTime,
            DistanceKm = raceEvent.DistanceKm,
            EventTypeId = raceEvent.EventTypeId,
            VenueName = raceEvent.VenueName,
            City = raceEvent.City,
            Province = raceEvent.Province,
            RegistrationDeadline = raceEvent.RegistrationDeadline,
            Status = EventStatuses.Open
        };

        var result = await controller.UpdateEvent(raceEvent.EventId, request);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);

        // The name must be unchanged in the database.
        Assert.Equal("Test Road Race", db.Events.First().Name);
    }

    [Fact]
    public async Task UpdateEvent_OnTheirOwnEvent_Succeeds()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var raceEvent = TestDatabase.TestEvent(db);

        var controller = new EventsController(db);
        TestDatabase.SignIn(controller, TestDatabase.Organiser(db), RoleNames.Organiser);

        var request = new UpdateEventRequest
        {
            Name = "Renamed Road Race",
            EventDate = raceEvent.EventDate,
            StartTime = raceEvent.StartTime,
            DistanceKm = raceEvent.DistanceKm,
            EventTypeId = raceEvent.EventTypeId,
            VenueName = raceEvent.VenueName,
            City = raceEvent.City,
            Province = raceEvent.Province,
            RegistrationDeadline = raceEvent.RegistrationDeadline,
            Status = EventStatuses.Closed
        };

        var result = await controller.UpdateEvent(raceEvent.EventId, request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var detail = Assert.IsType<EventDetailResponse>(ok.Value);

        Assert.Equal("Renamed Road Race", detail.Name);
        Assert.Equal(EventStatuses.Closed, detail.Status);
    }

    [Fact]
    public async Task DeleteEvent_OnAnotherOrganisersEvent_IsForbidden()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var raceEvent = TestDatabase.TestEvent(db);

        var controller = new EventsController(db);
        TestDatabase.SignIn(controller, TestDatabase.OtherOrganiser(db), RoleNames.Organiser);

        var result = await controller.DeleteEvent(raceEvent.EventId);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Single(db.Events);
    }

    [Fact]
    public async Task GetEvent_WithUnknownId_ReturnsNotFound()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);

        var controller = new EventsController(db);
        TestDatabase.SignIn(controller, null, null);

        var result = await controller.GetEvent(9999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}