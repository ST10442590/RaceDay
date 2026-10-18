using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;

namespace RaceDay.Api.Controllers;

// The fixed list of event types: Run, Walk and Cycle.
[ApiController]
[Route("api/event-types")]
[Produces("application/json")]
public class EventTypesController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventTypesController(RaceDayDbContext db) => _db = db;

    // Lists the event types, for filtering the event list and for filling in
    // the event type when creating an event.
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEventTypes()
    {
        var types = await _db.EventTypes
            .OrderBy(t => t.EventTypeId)
            .Select(t => new { t.EventTypeId, t.TypeName })
            .ToListAsync();

        return Ok(types);
    }
}