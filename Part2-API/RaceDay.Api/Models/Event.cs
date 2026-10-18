namespace RaceDay.Api.Models;

// A race, walk or cycle event created and owned by one Organiser.
public class Event
{
    public int EventId { get; set; }

    // The Organiser who created this event and may edit it.
    public int OrganiserId { get; set; }
    public int EventTypeId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly EventDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public decimal DistanceKm { get; set; }

    public string VenueName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;

    // Used by the live weather lookup.
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public string? RouteUrl { get; set; }

    // Azure Blob Storage URL for the event banner, set in Part 3.
    public string? ImageUrl { get; set; }

    public DateOnly RegistrationDeadline { get; set; }
    public string Status { get; set; } = EventStatuses.Upcoming;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User? Organiser { get; set; }
    public EventType? EventType { get; set; }
    public ICollection<EventCategory> Categories { get; set; } = new List<EventCategory>();
}

public static class EventStatuses
{
    public const string Upcoming = "Upcoming";
    public const string Open = "Open";
    public const string Closed = "Closed";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
        { Upcoming, Open, Closed, Completed, Cancelled };
}