using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Models.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

// Event enrolments. Participants enter events by selecting a category;
// Organisers view everyone entered in their own events.
[ApiController]
[Produces("application/json")]
public class EnrolmentsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    // Enters the logged-in Participant into an event in the chosen category.
    // Rejects: a category that belongs to a different event, an age outside
    // the category range, a closed or full category, and a second entry into
    // the same event.
    [HttpPost("api/enrolments")]
    [RequireRole(RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentResponse>> Enter(CreateEnrolmentRequest request)
    {
        var participantId = HttpContext.Session.GetUserId()!.Value;

        var raceEvent = await _db.Events
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventId == request.EventId);

        if (raceEvent is null)
        {
            return NotFound(new { message = $"No event with id {request.EventId}." });
        }

        var category = raceEvent.Categories.FirstOrDefault(c => c.CategoryId == request.CategoryId);
        if (category is null)
        {
            // The category may exist, but not on this event. The composite
            // foreign key in the database would also refuse this.
            return BadRequest(new
            {
                message = $"Category {request.CategoryId} does not belong to event {request.EventId}."
            });
        }

        if (raceEvent.Status is EventStatuses.Cancelled or EventStatuses.Completed)
        {
            return Conflict(new { message = $"This event is {raceEvent.Status.ToLowerInvariant()}." });
        }

        if (raceEvent.RegistrationDeadline < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return Conflict(new
            {
                message = $"Registration closed on {raceEvent.RegistrationDeadline:yyyy-MM-dd}."
            });
        }

        if (await _db.Enrolments.AnyAsync(
                e => e.ParticipantId == participantId && e.EventId == request.EventId))
        {
            return Conflict(new { message = "You have already entered this event." });
        }

        var participant = await _db.Users.FirstAsync(u => u.UserId == participantId);
        var ageError = CheckAge(participant, category, raceEvent.EventDate);
        if (ageError is not null)
        {
            return BadRequest(new { message = ageError });
        }

        var confirmed = await _db.Enrolments.CountAsync(
            e => e.CategoryId == category.CategoryId && e.Status == EnrolmentStatuses.Confirmed);

        if (confirmed >= category.MaxParticipants)
        {
            return Conflict(new { message = $"'{category.CategoryName}' is full." });
        }

        var entriesSoFar = await _db.Enrolments.CountAsync(e => e.EventId == request.EventId);

        var enrolment = new Enrolment
        {
            ParticipantId = participantId,
            EventId = request.EventId,
            CategoryId = request.CategoryId,
            RaceNumber = $"E{request.EventId}-{entriesSoFar + 1:000}",
            Status = EnrolmentStatuses.Confirmed
        };

        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMyEnrolments), null,
            ToResponse(enrolment, raceEvent, category));
    }

    // Lists the logged-in Participant's own enrolments.
    [HttpGet("api/enrolments/me")]
    [RequireRole(RoleNames.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetMyEnrolments()
    {
        var participantId = HttpContext.Session.GetUserId()!.Value;

        var enrolments = await _db.Enrolments
            .Include(e => e.Category)
                .ThenInclude(c => c!.Event)
            .Where(e => e.ParticipantId == participantId)
            .OrderBy(e => e.Category!.Event!.EventDate)
            .ToListAsync();

        return Ok(enrolments
            .Select(e => ToResponse(e, e.Category!.Event!, e.Category!))
            .ToList());
    }

    // Cancels the Participant's own enrolment. The row is kept and marked
    // Cancelled, so the Organiser still has a record of it.
    [HttpDelete("api/enrolments/{id:int}")]
    [RequireRole(RoleNames.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id)
    {
        var participantId = HttpContext.Session.GetUserId()!.Value;

        var enrolment = await _db.Enrolments
            .Include(e => e.Category)
                .ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);

        if (enrolment is null)
        {
            return NotFound(new { message = $"No enrolment with id {id}." });
        }

        if (enrolment.ParticipantId != participantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only cancel your own enrolments." });
        }

        var deadline = enrolment.Category!.Event!.RegistrationDeadline;
        if (deadline < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return Conflict(new
            {
                message = $"Registration closed on {deadline:yyyy-MM-dd}, so this entry can no longer be cancelled."
            });
        }

        enrolment.Status = EnrolmentStatuses.Cancelled;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // Lists everyone entered in one of the logged-in Organiser's own events,
    // ordered by category.
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [RequireRole(RoleNames.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EnrolmentDetailResponse>>> GetForEvent(int eventId)
    {
        var organiserId = HttpContext.Session.GetUserId()!.Value;

        var raceEvent = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (raceEvent is null)
        {
            return NotFound(new { message = $"No event with id {eventId}." });
        }

        if (raceEvent.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only view enrolments for events that you created." });
        }

        var enrolments = await _db.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Category)
            .Include(e => e.Result)
            .Where(e => e.EventId == eventId)
            .OrderBy(e => e.CategoryId)
            .ThenBy(e => e.RaceNumber)
            .ToListAsync();

        var list = enrolments.Select(e => new EnrolmentDetailResponse
        {
            EnrolmentId = e.EnrolmentId,
            EventId = e.EventId,
            EventName = raceEvent.Name,
            EventDate = raceEvent.EventDate,
            CategoryId = e.CategoryId,
            CategoryName = e.Category!.CategoryName,
            RaceNumber = e.RaceNumber,
            Status = e.Status,
            EnrolledAt = e.EnrolledAt,
            ParticipantId = e.ParticipantId,
            ParticipantName = $"{e.Participant!.FirstName} {e.Participant.LastName}",
            ParticipantEmail = e.Participant.Email,
            HasResult = e.Result is not null
        }).ToList();

        return Ok(list);
    }

    // Checks the participant's age on the day of the event against the
    // category's range. Returns null when the entry is allowed.
    private static string? CheckAge(User participant, EventCategory category, DateOnly eventDate)
    {
        if (category.MinAge == 0 && category.MaxAge is null)
        {
            return null;
        }

        if (participant.DateOfBirth is null)
        {
            return "This category has an age restriction, so add your date of birth to your profile first.";
        }

        var dob = participant.DateOfBirth.Value;
        var age = eventDate.Year - dob.Year;
        if (dob > eventDate.AddYears(-age))
        {
            age--;
        }

        if (age < category.MinAge)
        {
            return $"'{category.CategoryName}' is for ages {category.MinAge} and up; you will be {age} on race day.";
        }

        if (category.MaxAge is not null && age > category.MaxAge)
        {
            return $"'{category.CategoryName}' is for ages {category.MinAge} to {category.MaxAge}; you will be {age} on race day.";
        }

        return null;
    }

    internal static EnrolmentResponse ToResponse(Enrolment e, Event raceEvent, EventCategory category) => new()
    {
        EnrolmentId = e.EnrolmentId,
        EventId = e.EventId,
        EventName = raceEvent.Name,
        EventDate = raceEvent.EventDate,
        CategoryId = e.CategoryId,
        CategoryName = category.CategoryName,
        RaceNumber = e.RaceNumber,
        Status = e.Status,
        EnrolledAt = e.EnrolledAt
    };
}