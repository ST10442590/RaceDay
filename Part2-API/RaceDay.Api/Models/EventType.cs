namespace RaceDay.Api.Models;

// Lookup table of event types: Run, Walk or Cycle.
public class EventType
{
    public int EventTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
