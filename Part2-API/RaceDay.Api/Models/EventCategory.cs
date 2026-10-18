namespace RaceDay.Api.Models;

// An age or distance category within an event, for example
// "Under 20", "Senior", "10km" or "21.1km Half Marathon".
public class EventCategory
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    // Either "Age" or "Distance". See <see cref="CategoryTypes"/>.
    public string CategoryType { get; set; } = CategoryTypes.Distance;

    // Required for a Distance category, null for an Age category.
    public decimal? DistanceKm { get; set; }

    public int MinAge { get; set; }
    public int? MaxAge { get; set; }

    public decimal EntryFee { get; set; }
    public int MaxParticipants { get; set; }
    public TimeOnly? StartTime { get; set; }

    public Event? Event { get; set; }
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public static class CategoryTypes
{
    public const string Age = "Age";
    public const string Distance = "Distance";

    public static readonly string[] All = { Age, Distance };
}