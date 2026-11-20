using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Models.Dtos;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

// Event banner images. The MVC site sends the file here; the API stores it in
// Azure Blob Storage and saves the blob's URL in the event's ImageUrl column.
[ApiController]
[Produces("application/json")]
public class EventImagesController : ControllerBase
{
    public const long MaxImageBytes = 5 * 1024 * 1024;

    // Content types accepted, with the file extension used for the blob name.
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private readonly RaceDayDbContext _db;
    private readonly IImageStorage _storage;

    public EventImagesController(RaceDayDbContext db, IImageStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    // Uploads or replaces the banner of one of the logged-in Organiser's events.
    // Send multipart/form-data with the image in a field named "File".
    // JPG, PNG or WebP, up to 5 MB. Returns the updated event, including ImageUrl.
    // 400 for a missing, oversized or wrong-type file; 403 for another
    // Organiser's event; 503 if Azure Blob Storage is not configured.
    [HttpPost("api/events/{id:int}/image")]
    [RequireRole(RoleNames.Organiser)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxImageBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EventDetailResponse>> UploadImage(int id, [FromForm] EventImageUploadRequest request)
    {
        var raceEvent = await LoadEventAsync(id);
        if (raceEvent is null)
        {
            return NotFound(new { message = $"No event with id {id}." });
        }

        var ownershipError = CheckOwnership(raceEvent);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        var file = request.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose an image file to upload." });
        }

        if (file.Length > MaxImageBytes)
        {
            return BadRequest(new { message = "The image must be 5 MB or smaller." });
        }

        if (!AllowedTypes.TryGetValue(file.ContentType ?? string.Empty, out var extension))
        {
            return BadRequest(new { message = "Upload a JPG, PNG or WebP image." });
        }

        if (!_storage.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Image storage is not configured. Set AzureStorage:ConnectionString for the API."
            });
        }

        // A new, unique blob name every time, so browsers never show a cached
        // copy of the old banner.
        var blobName = $"event-{id}/{Guid.NewGuid():N}{extension}";

        string imageUrl;
        try
        {
            await using var stream = file.OpenReadStream();
            imageUrl = await _storage.UploadAsync(stream, blobName, file.ContentType!);
        }
        catch (ImageStorageException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }

        var previousUrl = raceEvent.ImageUrl;

        raceEvent.ImageUrl = imageUrl;
        raceEvent.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Tidy up the replaced banner so the container does not fill with old images.
        if (!string.IsNullOrEmpty(previousUrl))
        {
            await _storage.DeleteAsync(previousUrl);
        }

        return Ok(EventsController.ToDetail(raceEvent, await CountConfirmedByCategoryAsync(id)));
    }

    // Removes the banner from one of the logged-in Organiser's events.
    [HttpDelete("api/events/{id:int}/image")]
    [RequireRole(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(int id)
    {
        var raceEvent = await LoadEventAsync(id);
        if (raceEvent is null)
        {
            return NotFound(new { message = $"No event with id {id}." });
        }

        var ownershipError = CheckOwnership(raceEvent);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        if (!string.IsNullOrEmpty(raceEvent.ImageUrl))
        {
            await _storage.DeleteAsync(raceEvent.ImageUrl);
            raceEvent.ImageUrl = null;
            raceEvent.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    private Task<Event?> LoadEventAsync(int id) =>
        _db.Events
            .Include(e => e.EventType)
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventId == id);

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
            message = "You can only manage events that you created."
        })
        { StatusCode = StatusCodes.Status403Forbidden };
    }

    private async Task<Dictionary<int, int>> CountConfirmedByCategoryAsync(int eventId)
    {
        return await _db.Enrolments
            .Where(e => e.EventId == eventId && e.Status == EnrolmentStatuses.Confirmed)
            .GroupBy(e => e.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);
    }
}