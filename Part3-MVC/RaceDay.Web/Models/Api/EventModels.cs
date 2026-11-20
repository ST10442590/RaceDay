namespace RaceDay.Web.Models.Api;

public static class EventStatuses
{
    public const string Upcoming = "Upcoming";
    public const string Open = "Open";
    public const string Closed = "Closed";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Upcoming, Open, Closed, Completed, Cancelled };
}

public static class CategoryTypes
{
    public const string Age = "Age";
    public const string Distance = "Distance";
}

// One row from GET /api/event-types.
public class EventTypeItem
{
    public int EventTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
}

// An event in a list, from GET /api/events and GET /api/events/mine.
public class EventSummary
{
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public decimal DistanceKm { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public DateOnly RegistrationDeadline { get; set; }
    public string Status { get; set; } = string.Empty;
    public int CategoryCount { get; set; }

    // Azure Blob Storage URL of the event banner, if one has been uploaded.
    public string? ImageUrl { get; set; }
}

// A single event, from GET /api/events/{id}.
public class EventDetail : EventSummary
{
    public string? Description { get; set; }
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? RouteUrl { get; set; }
    public List<Category> Categories { get; set; } = new();
}

// Sent to POST /api/events and PUT /api/events/{id}.
public class EventRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly EventDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public decimal DistanceKm { get; set; }
    public int EventTypeId { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? RouteUrl { get; set; }
    public DateOnly RegistrationDeadline { get; set; }

    // Only used when updating.
    public string Status { get; set; } = EventStatuses.Upcoming;
}

// An age or distance category within an event.
public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryType { get; set; } = string.Empty;
    public decimal? DistanceKm { get; set; }
    public int MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal EntryFee { get; set; }
    public int MaxParticipants { get; set; }
    public TimeOnly? StartTime { get; set; }
    public int PlacesRemaining { get; set; }

    public string AgeRange => MaxAge is null ? $"{MinAge}+" : $"{MinAge} – {MaxAge}";
}

// Sent to POST /api/events/{eventId}/categories and PUT /api/categories/{id}.
public class CategoryRequest
{
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryType { get; set; } = CategoryTypes.Distance;
    public decimal? DistanceKm { get; set; }
    public int MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal EntryFee { get; set; }
    public int MaxParticipants { get; set; }
    public TimeOnly? StartTime { get; set; }
}

// Weather at the venue, from GET /api/events/{id}/weather.
public class WeatherInfo
{
    public DateOnly Date { get; set; }
    public bool IsRaceDayForecast { get; set; }
    public decimal Temperature { get; set; }
    public decimal MinTemperature { get; set; }
    public string Conditions { get; set; } = string.Empty;
    public decimal WindSpeed { get; set; }
    public int RainChance { get; set; }
}

