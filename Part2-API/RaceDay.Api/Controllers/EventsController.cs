using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Models.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

// Events. Both roles may browse and view events. Only Organisers may create
// them, and only on their own events may they make changes.
[ApiController]
[Route("api/events")]
[Produces("application/json")]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db) => _db = db;

    // Lists events for browsing, earliest race date first. Open to everyone,
    // including visitors who are not logged in. All filters are optional.
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EventSummaryResponse>>> GetEvents(
        [FromQuery] string? type,
        [FromQuery] string? city,
        [FromQuery] string? province,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? status)
    {
        var query = _db.Events
            .Include(e => e.EventType)
            .Include(e => e.Categories)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(e => e.EventType!.TypeName == type);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(e => e.City == city);
        }

        if (!string.IsNullOrWhiteSpace(province))
        {
            query = query.Where(e => e.Province == province);
        }

        if (from is not null)
        {
            query = query.Where(e => e.EventDate >= from);
        }

        if (to is not null)
        {
            query = query.Where(e => e.EventDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(e => e.Status == status);
        }

        var events = await query
            .OrderBy(e => e.EventDate)
            .Select(e => ToSummary(e))
            .ToListAsync();

        return Ok(events);
    }

    // Returns one event with all of its categories. 404 if it does not exist.
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailResponse>> GetEvent(int id)
    {
        var raceEvent = await _db.Events
            .Include(e => e.EventType)
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (raceEvent is null)
        {
            return NotFound(new { message = $"No event with id {id}." });
        }

        var enrolmentCounts = await CountConfirmedByCategoryAsync(id);

        return Ok(ToDetail(raceEvent, enrolmentCounts));
    }

    // Lists the events created by the logged-in Organiser.
    // 401 if nobody is logged in, 403 if the user is a Participant.
    [HttpGet("mine")]
    [RequireRole(RoleNames.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EventSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EventSummaryResponse>>> GetMyEvents()
    {
        var organiserId = HttpContext.Session.GetUserId()!.Value;

        var events = await _db.Events
            .Include(e => e.EventType)
            .Include(e => e.Categories)
            .Where(e => e.OrganiserId == organiserId)
            .OrderBy(e => e.EventDate)
            .Select(e => ToSummary(e))
            .ToListAsync();

        return Ok(events);
    }

    // Creates an event owned by the logged-in Organiser. The owner is taken
    // from the session, so an Organiser cannot create an event in someone
    // else's name. 400 if validation fails, 401/403 on the role check.
    [HttpPost]
    [RequireRole(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventDetailResponse>> CreateEvent(CreateEventRequest request)
    {
        var organiserId = HttpContext.Session.GetUserId()!.Value;

        if (!await _db.EventTypes.AnyAsync(t => t.EventTypeId == request.EventTypeId))
        {
            return BadRequest(new { message = "Unknown event type. See GET /api/event-types." });
        }

        if (request.RegistrationDeadline > request.EventDate)
        {
            return BadRequest(new { message = "Registration must close on or before the event date." });
        }

        var raceEvent = new Event
        {
            OrganiserId = organiserId,
            EventTypeId = request.EventTypeId,
            Name = request.Name.Trim(),
            Description = request.Description,
            EventDate = request.EventDate,
            StartTime = request.StartTime,
            DistanceKm = request.DistanceKm,
            VenueName = request.VenueName.Trim(),
            City = request.City.Trim(),
            Province = request.Province.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            RouteUrl = request.RouteUrl,
            RegistrationDeadline = request.RegistrationDeadline,
            Status = EventStatuses.Upcoming
        };

        _db.Events.Add(raceEvent);
        await _db.SaveChangesAsync();

        await _db.Entry(raceEvent).Reference(e => e.EventType).LoadAsync();
        await _db.Entry(raceEvent).Reference(e => e.Organiser).LoadAsync();

        var detail = ToDetail(raceEvent, new Dictionary<int, int>());
        return CreatedAtAction(nameof(GetEvent), new { id = raceEvent.EventId }, detail);
    }

    // Updates one of the logged-in Organiser's own events, including its
    // status. 403 if the event belongs to a different Organiser.
    [HttpPut("{id:int}")]
    [RequireRole(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailResponse>> UpdateEvent(int id, UpdateEventRequest request)
    {
        var raceEvent = await _db.Events
            .Include(e => e.EventType)
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (raceEvent is null)
        {
            return NotFound(new { message = $"No event with id {id}." });
        }

        var ownershipError = CheckOwnership(raceEvent);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        if (!EventStatuses.All.Contains(request.Status))
        {
            return BadRequest(new
            {
                message = $"Status must be one of: {string.Join(", ", EventStatuses.All)}."
            });
        }

        if (!await _db.EventTypes.AnyAsync(t => t.EventTypeId == request.EventTypeId))
        {
            return BadRequest(new { message = "Unknown event type. See GET /api/event-types." });
        }

        if (request.RegistrationDeadline > request.EventDate)
        {
            return BadRequest(new { message = "Registration must close on or before the event date." });
        }

        raceEvent.EventTypeId = request.EventTypeId;
        raceEvent.Name = request.Name.Trim();
        raceEvent.Description = request.Description;
        raceEvent.EventDate = request.EventDate;
        raceEvent.StartTime = request.StartTime;
        raceEvent.DistanceKm = request.DistanceKm;
        raceEvent.VenueName = request.VenueName.Trim();
        raceEvent.City = request.City.Trim();
        raceEvent.Province = request.Province.Trim();
        raceEvent.Latitude = request.Latitude;
        raceEvent.Longitude = request.Longitude;
        raceEvent.RouteUrl = request.RouteUrl;
        raceEvent.RegistrationDeadline = request.RegistrationDeadline;
        raceEvent.Status = request.Status;
        raceEvent.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _db.Entry(raceEvent).Reference(e => e.EventType).LoadAsync();

        var enrolmentCounts = await CountConfirmedByCategoryAsync(id);
        return Ok(ToDetail(raceEvent, enrolmentCounts));
    }

    // Deletes one of the logged-in Organiser's own events, along with its
    // categories. An event that people have already entered cannot be
    // deleted; set its status to Cancelled instead, so the record survives.
    [HttpDelete("{id:int}")]
    [RequireRole(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var raceEvent = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (raceEvent is null)
        {
            return NotFound(new { message = $"No event with id {id}." });
        }

        var ownershipError = CheckOwnership(raceEvent);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        var confirmed = await _db.Enrolments
            .CountAsync(e => e.EventId == id && e.Status == EnrolmentStatuses.Confirmed);

        if (confirmed > 0)
        {
            return Conflict(new
            {
                message = $"This event has {confirmed} confirmed enrolment(s). " +
                          "Set its status to Cancelled instead of deleting it."
            });
        }

        _db.Events.Remove(raceEvent);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // Returns 403 when the logged-in Organiser does not own this event,
    // and null when they do.
    private ObjectResult? CheckOwnership(Event raceEvent)
    {
        var organiserId = HttpContext.Session.GetUserId()!.Value;

        if (raceEvent.OrganiserId == organiserId)
        {
            return null;
        }

        return new ObjectResult(new
        {
            message = "You can only manage events that you created."
        })
        { StatusCode = StatusCodes.Status403Forbidden };
    }

    // Confirmed enrolments per category for one event.
    private async Task<Dictionary<int, int>> CountConfirmedByCategoryAsync(int eventId)
    {
        return await _db.Enrolments
            .Where(e => e.EventId == eventId && e.Status == EnrolmentStatuses.Confirmed)
            .GroupBy(e => e.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);
    }

    internal static EventSummaryResponse ToSummary(Event e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        EventType = e.EventType!.TypeName,
        EventDate = e.EventDate,
        StartTime = e.StartTime,
        DistanceKm = e.DistanceKm,
        VenueName = e.VenueName,
        City = e.City,
        Province = e.Province,
        RegistrationDeadline = e.RegistrationDeadline,
        Status = e.Status,
        CategoryCount = e.Categories.Count
    };

    internal static EventDetailResponse ToDetail(Event e, Dictionary<int, int> confirmedByCategory) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        EventType = e.EventType!.TypeName,
        EventDate = e.EventDate,
        StartTime = e.StartTime,
        DistanceKm = e.DistanceKm,
        VenueName = e.VenueName,
        City = e.City,
        Province = e.Province,
        RegistrationDeadline = e.RegistrationDeadline,
        Status = e.Status,
        CategoryCount = e.Categories.Count,
        Description = e.Description,
        OrganiserId = e.OrganiserId,
        OrganiserName = e.Organiser is null
            ? string.Empty
            : $"{e.Organiser.FirstName} {e.Organiser.LastName}",
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        RouteUrl = e.RouteUrl,
        ImageUrl = e.ImageUrl,
        Categories = e.Categories
            .OrderBy(c => c.CategoryId)
            .Select(c => CategoriesController.ToResponse(
                c, confirmedByCategory.GetValueOrDefault(c.CategoryId)))
            .ToList()
    };
}