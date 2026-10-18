using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Models.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

// Age and distance categories within an event, for example "Under 20",
// "Senior", "10km" or "21.1km Half Marathon". Both roles may view them;
// only the Organiser who owns the event may change them.
[ApiController]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db) => _db = db;

    // Lists the available categories of an event, with places remaining.
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
        {
            return NotFound(new { message = $"No event with id {eventId}." });
        }

        var categories = await _db.EventCategories
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.CategoryId)
            .ToListAsync();

        var confirmed = await CountConfirmedAsync(eventId);

        return Ok(categories
            .Select(c => ToResponse(c, confirmed.GetValueOrDefault(c.CategoryId)))
            .ToList());
    }

    // Returns one category.
    [HttpGet("api/categories/{id:int}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> GetCategory(int id)
    {
        var category = await _db.EventCategories.FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null)
        {
            return NotFound(new { message = $"No category with id {id}." });
        }

        var confirmed = await _db.Enrolments.CountAsync(
            e => e.CategoryId == id && e.Status == EnrolmentStatuses.Confirmed);

        return Ok(ToResponse(category, confirmed));
    }

    // Defines an age or distance category on one of the logged-in Organiser's
    // own events. 409 if the name is already used on this event.
    [HttpPost("api/events/{eventId:int}/categories")]
    [RequireRole(RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> CreateCategory(
        int eventId, CreateCategoryRequest request)
    {
        var raceEvent = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (raceEvent is null)
        {
            return NotFound(new { message = $"No event with id {eventId}." });
        }

        var ownershipError = CheckOwnership(raceEvent);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        var validationError = ValidateCategory(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var name = request.CategoryName.Trim();
        if (await _db.EventCategories.AnyAsync(c => c.EventId == eventId && c.CategoryName == name))
        {
            return Conflict(new
            {
                message = $"This event already has a category called '{name}'."
            });
        }

        var category = new EventCategory
        {
            EventId = eventId,
            CategoryName = name,
            CategoryType = request.CategoryType,
            DistanceKm = request.DistanceKm,
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,
            EntryFee = request.EntryFee,
            MaxParticipants = request.MaxParticipants,
            StartTime = request.StartTime
        };

        _db.EventCategories.Add(category);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCategory),
            new { id = category.CategoryId }, ToResponse(category, 0));
    }

    // Updates a category on one of the logged-in Organiser's own events.
    [HttpPut("api/categories/{id:int}")]
    [RequireRole(RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> UpdateCategory(
        int id, UpdateCategoryRequest request)
    {
        var category = await _db.EventCategories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category is null)
        {
            return NotFound(new { message = $"No category with id {id}." });
        }

        var ownershipError = CheckOwnership(category.Event!);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        var validationError = ValidateCategory(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var confirmed = await _db.Enrolments.CountAsync(
            e => e.CategoryId == id && e.Status == EnrolmentStatuses.Confirmed);

        if (request.MaxParticipants < confirmed)
        {
            return BadRequest(new
            {
                message = $"{confirmed} participant(s) have already entered this category, " +
                          "so the cap cannot be set below that."
            });
        }

        var name = request.CategoryName.Trim();
        if (await _db.EventCategories.AnyAsync(
                c => c.EventId == category.EventId && c.CategoryName == name && c.CategoryId != id))
        {
            return Conflict(new
            {
                message = $"This event already has a category called '{name}'."
            });
        }

        category.CategoryName = name;
        category.CategoryType = request.CategoryType;
        category.DistanceKm = request.DistanceKm;
        category.MinAge = request.MinAge;
        category.MaxAge = request.MaxAge;
        category.EntryFee = request.EntryFee;
        category.MaxParticipants = request.MaxParticipants;
        category.StartTime = request.StartTime;

        await _db.SaveChangesAsync();

        return Ok(ToResponse(category, confirmed));
    }

    // Removes a category from one of the logged-in Organiser's own events.
    // 409 if participants have already entered it.
    [HttpDelete("api/categories/{id:int}")]
    [RequireRole(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.EventCategories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category is null)
        {
            return NotFound(new { message = $"No category with id {id}." });
        }

        var ownershipError = CheckOwnership(category.Event!);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        if (await _db.Enrolments.AnyAsync(e => e.CategoryId == id))
        {
            return Conflict(new
            {
                message = "Participants have already entered this category, so it cannot be deleted."
            });
        }

        _db.EventCategories.Remove(category);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // Checks the rule the database also enforces: a Distance category needs a
    // distance, an Age category must not have one, and the age range must
    // make sense. Returns null when the request is valid.
    private static string? ValidateCategory(CreateCategoryRequest request)
    {
        if (!CategoryTypes.All.Contains(request.CategoryType))
        {
            return $"CategoryType must be one of: {string.Join(", ", CategoryTypes.All)}.";
        }

        if (request.CategoryType == CategoryTypes.Distance &&
            (request.DistanceKm is null || request.DistanceKm <= 0))
        {
            return "A Distance category must have a DistanceKm greater than zero.";
        }

        if (request.CategoryType == CategoryTypes.Age && request.DistanceKm is not null)
        {
            return "An Age category must not have a DistanceKm.";
        }

        if (request.MaxAge is not null && request.MaxAge < request.MinAge)
        {
            return "MaxAge cannot be lower than MinAge.";
        }

        return null;
    }

    // Returns 403 when the logged-in Organiser does not own this event.
    private ObjectResult? CheckOwnership(Event raceEvent)
    {
        var organiserId = HttpContext.Session.GetUserId()!.Value;

        if (raceEvent.OrganiserId == organiserId)
        {
            return null;
        }

        return new ObjectResult(new
        {
            message = "You can only manage categories on events that you created."
        })
        { StatusCode = StatusCodes.Status403Forbidden };
    }

    private async Task<Dictionary<int, int>> CountConfirmedAsync(int eventId)
    {
        return await _db.Enrolments
            .Where(e => e.EventId == eventId && e.Status == EnrolmentStatuses.Confirmed)
            .GroupBy(e => e.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);
    }

    internal static CategoryResponse ToResponse(EventCategory c, int confirmedEnrolments) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        CategoryName = c.CategoryName,
        CategoryType = c.CategoryType,
        DistanceKm = c.DistanceKm,
        MinAge = c.MinAge,
        MaxAge = c.MaxAge,
        EntryFee = c.EntryFee,
        MaxParticipants = c.MaxParticipants,
        StartTime = c.StartTime,
        PlacesRemaining = Math.Max(0, c.MaxParticipants - confirmedEnrolments)
    };
}