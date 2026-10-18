using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Models.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

// Results. Organisers capture finish times and finishing positions for their
// own events; Participants view their own results; the results board for an
// event is public.
[ApiController]
[Produces("application/json")]
public class ResultsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db) => _db = db;

    // Captures a participant's result for one enrolment. A Finished result
    // needs both a time and a position; DNF, DNS and DQ have neither.
    [HttpPost("api/results")]
    [RequireRole(RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> CaptureResult(CreateResultRequest request)
    {
        var organiserId = HttpContext.Session.GetUserId()!.Value;

        var enrolment = await LoadEnrolmentAsync(request.EnrolmentId);
        if (enrolment is null)
        {
            return NotFound(new { message = $"No enrolment with id {request.EnrolmentId}." });
        }

        if (enrolment.Category!.Event!.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only capture results for events that you created." });
        }

        if (enrolment.Status == EnrolmentStatuses.Cancelled)
        {
            return BadRequest(new { message = "This enrolment was cancelled, so it has no result." });
        }

        var validationError = Validate(request.ResultStatus, request.FinishTime, request.FinishPosition);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        if (await _db.Results.AnyAsync(r => r.EnrolmentId == request.EnrolmentId))
        {
            return Conflict(new
            {
                message = "A result has already been captured for this enrolment. Use PUT to correct it."
            });
        }

        var result = new Result
        {
            EnrolmentId = request.EnrolmentId,
            FinishTime = request.FinishTime,
            FinishPosition = request.FinishPosition,
            ResultStatus = request.ResultStatus,
            RecordedById = organiserId
        };

        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEventResults),
            new { eventId = enrolment.EventId }, ToResponse(result, enrolment));
    }

    // Corrects a previously captured result.
    [HttpPut("api/results/{id:int}")]
    [RequireRole(RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultResponse>> UpdateResult(int id, UpdateResultRequest request)
    {
        var organiserId = HttpContext.Session.GetUserId()!.Value;

        var result = await _db.Results.FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null)
        {
            return NotFound(new { message = $"No result with id {id}." });
        }

        var enrolment = await LoadEnrolmentAsync(result.EnrolmentId);
        if (enrolment!.Category!.Event!.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only correct results for events that you created." });
        }

        var validationError = Validate(request.ResultStatus, request.FinishTime, request.FinishPosition);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        result.FinishTime = request.FinishTime;
        result.FinishPosition = request.FinishPosition;
        result.ResultStatus = request.ResultStatus;
        result.RecordedById = organiserId;
        result.RecordedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ToResponse(result, enrolment));
    }

    // Removes a result captured in error.
    [HttpDelete("api/results/{id:int}")]
    [RequireRole(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteResult(int id)
    {
        var organiserId = HttpContext.Session.GetUserId()!.Value;

        var result = await _db.Results.FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null)
        {
            return NotFound(new { message = $"No result with id {id}." });
        }

        var enrolment = await LoadEnrolmentAsync(result.EnrolmentId);
        if (enrolment!.Category!.Event!.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only delete results for events that you created." });
        }

        _db.Results.Remove(result);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // The results board for an event, ordered by category then position.
    // Open to everyone.
    [HttpGet("api/events/{eventId:int}/results")]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetEventResults(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
        {
            return NotFound(new { message = $"No event with id {eventId}." });
        }

        var results = await _db.Results
            .Include(r => r.Enrolment)
                .ThenInclude(e => e!.Participant)
            .Include(r => r.Enrolment)
                .ThenInclude(e => e!.Category)
                    .ThenInclude(c => c!.Event)
            .Where(r => r.Enrolment!.EventId == eventId)
            .ToListAsync();

        var board = results
            .OrderBy(r => r.Enrolment!.CategoryId)
            .ThenBy(r => r.FinishPosition ?? int.MaxValue)
            .Select(r => ToResponse(r, r.Enrolment!))
            .ToList();

        return Ok(board);
    }

    // The logged-in Participant's own results across every event entered.
    [HttpGet("api/results/me")]
    [RequireRole(RoleNames.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetMyResults()
    {
        var participantId = HttpContext.Session.GetUserId()!.Value;

        var results = await _db.Results
            .Include(r => r.Enrolment)
                .ThenInclude(e => e!.Participant)
            .Include(r => r.Enrolment)
                .ThenInclude(e => e!.Category)
                    .ThenInclude(c => c!.Event)
            .Where(r => r.Enrolment!.ParticipantId == participantId)
            .ToListAsync();

        var mine = results
            .OrderByDescending(r => r.Enrolment!.Category!.Event!.EventDate)
            .Select(r => ToResponse(r, r.Enrolment!))
            .ToList();

        return Ok(mine);
    }

    // Loads an enrolment with the participant, category and event attached.
    private Task<Enrolment?> LoadEnrolmentAsync(int enrolmentId)
    {
        return _db.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Category)
                .ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);
    }

    // Mirrors the check constraint in the database: a Finished result must
    // have a time and a position, anything else must have neither.
    private static string? Validate(string status, TimeOnly? finishTime, int? finishPosition)
    {
        if (!ResultStatuses.All.Contains(status))
        {
            return $"ResultStatus must be one of: {string.Join(", ", ResultStatuses.All)}.";
        }

        if (status == ResultStatuses.Finished)
        {
            if (finishTime is null || finishPosition is null)
            {
                return "A Finished result needs both a FinishTime and a FinishPosition.";
            }
        }
        else if (finishTime is not null || finishPosition is not null)
        {
            return $"A {status} result must not have a FinishTime or a FinishPosition.";
        }

        return null;
    }

    internal static ResultResponse ToResponse(Result r, Enrolment enrolment) => new()
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,
        EventId = enrolment.EventId,
        EventName = enrolment.Category!.Event!.Name,
        CategoryId = enrolment.CategoryId,
        CategoryName = enrolment.Category.CategoryName,
        ParticipantId = enrolment.ParticipantId,
        ParticipantName = enrolment.Participant is null
            ? string.Empty
            : $"{enrolment.Participant.FirstName} {enrolment.Participant.LastName}",
        RaceNumber = enrolment.RaceNumber,
        FinishTime = r.FinishTime,
        FinishPosition = r.FinishPosition,
        ResultStatus = r.ResultStatus,
        RecordedAt = r.RecordedAt
    };
}