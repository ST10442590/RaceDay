using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models.Dtos;

// Details supplied when an Organiser creates an event.
public class CreateEventRequest
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public DateOnly EventDate { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    // Headline distance of the event, in kilometres.
    [Range(0.01, 9999)]
    public decimal DistanceKm { get; set; }

    // 1 = Run, 2 = Walk, 3 = Cycle. See GET /api/event-types.
    [Required]
    public int EventTypeId { get; set; }

    [Required, MaxLength(120)]
    public string VenueName { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string City { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Province { get; set; } = string.Empty;

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    [MaxLength(500)]
    public string? RouteUrl { get; set; }

    [Required]
    public DateOnly RegistrationDeadline { get; set; }
}

// Details supplied when an Organiser updates one of their events.
public class UpdateEventRequest : CreateEventRequest
{
    // Upcoming, Open, Closed, Completed or Cancelled.
    [Required]
    public string Status { get; set; } = EventStatuses.Upcoming;
}

// An event as it appears in a browse list.
public class EventSummaryResponse
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
}

// A single event with everything needed for its detail page.
public class EventDetailResponse : EventSummaryResponse
{
    public string? Description { get; set; }
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? RouteUrl { get; set; }
    public string? ImageUrl { get; set; }
    public List<CategoryResponse> Categories { get; set; } = new();
}

// An age or distance category within an event.
public class CategoryResponse
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

    // Places left, after confirmed enrolments.
    public int PlacesRemaining { get; set; }
}

// Details supplied when defining a category on an event.
public class CreateCategoryRequest
{
    [Required, MaxLength(80)]
    public string CategoryName { get; set; } = string.Empty;

    // "Age" or "Distance".
    [Required]
    public string CategoryType { get; set; } = CategoryTypes.Distance;

    // Required for a Distance category; leave null for an Age category.
    public decimal? DistanceKm { get; set; }

    [Range(0, 120)]
    public int MinAge { get; set; }

    [Range(0, 120)]
    public int? MaxAge { get; set; }

    [Range(0, 100000)]
    public decimal EntryFee { get; set; }

    [Range(1, 100000)]
    public int MaxParticipants { get; set; }

    public TimeOnly? StartTime { get; set; }
}

// Details supplied when updating a category.
public class UpdateCategoryRequest : CreateCategoryRequest
{
}