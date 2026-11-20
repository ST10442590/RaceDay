using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models.ViewModels;

// The home page: event browsing with optional filters.
public class HomeViewModel
{
    public List<EventSummary> Events { get; set; } = new();
    public List<EventTypeItem> EventTypes { get; set; } = new();

    public string? Type { get; set; }
    public string? Province { get; set; }
    public string? Search { get; set; }
    public bool IncludePast { get; set; }

    public string? ErrorMessage { get; set; }

    public static readonly string[] Provinces =
    {
        "Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal", "Limpopo",
        "Mpumalanga", "North West", "Northern Cape", "Western Cape"
    };
}

// The public event detail page, with the Enter Event form for Participants.
public class EventDetailsViewModel
{
    public EventDetail Event { get; set; } = new();

    // Set when the logged-in Participant has already entered this event.
    public Enrolment? MyEnrolment { get; set; }

    // Live weather at the venue; null if unavailable.
    public WeatherInfo? Weather { get; set; }

    public bool RegistrationOpen =>
        Event.Status == EventStatuses.Open &&
        Event.RegistrationDeadline >= DateOnly.FromDateTime(DateTime.Today);
}

// The public results board of one event.
public class EventResultsViewModel
{
    public EventDetail Event { get; set; } = new();
    public List<RaceResult> Results { get; set; } = new();
}

// One of the logged-in Participant's results, with the field size so it can
// read "47th of 312".
public class MyResultRow
{
    public RaceResult Result { get; set; } = new();
    public DateOnly EventDate { get; set; }
    public int FinishersInCategory { get; set; }
}

// "My events" for a Participant: upcoming and past entries.
public class MyEventsViewModel
{
    public List<Enrolment> Upcoming { get; set; } = new();
    public List<Enrolment> Past { get; set; } = new();
    public HashSet<int> EnrolmentsWithResults { get; set; } = new();
}

// The create and edit event form.
public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required, MaxLength(120), Display(Name = "Event name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required, Display(Name = "Event date")]
    public DateOnly EventDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddMonths(2));

    [Required, Display(Name = "Start time")]
    public TimeOnly StartTime { get; set; } = new(6, 30);

    [Range(0.01, 9999), Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; } = 10;

    [Required, Display(Name = "Event type")]
    public int EventTypeId { get; set; } = 1;

    [Required, MaxLength(120), Display(Name = "Venue")]
    public string VenueName { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string City { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Province { get; set; } = "Gauteng";

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    [MaxLength(500), Url, Display(Name = "Route map link")]
    public string? RouteUrl { get; set; }

    [Required, Display(Name = "Registration closes")]
    public DateOnly RegistrationDeadline { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddMonths(1));

    public string Status { get; set; } = EventStatuses.Upcoming;

    public List<EventTypeItem> EventTypes { get; set; } = new();

    public bool IsEdit => EventId is not null;

    public EventRequest ToRequest() => new()
    {
        Name = Name,
        Description = Description,
        EventDate = EventDate,
        StartTime = StartTime,
        DistanceKm = DistanceKm,
        EventTypeId = EventTypeId,
        VenueName = VenueName,
        City = City,
        Province = Province,
        Latitude = Latitude,
        Longitude = Longitude,
        RouteUrl = RouteUrl,
        RegistrationDeadline = RegistrationDeadline,
        Status = Status
    };

    public static EventFormViewModel From(EventDetail e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        Description = e.Description,
        EventDate = e.EventDate,
        StartTime = e.StartTime,
        DistanceKm = e.DistanceKm,
        VenueName = e.VenueName,
        City = e.City,
        Province = e.Province,
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        RouteUrl = e.RouteUrl,
        RegistrationDeadline = e.RegistrationDeadline,
        Status = e.Status
    };
}

// One row on the Organiser dashboard.
public class DashboardEventRow
{
    public EventSummary Event { get; set; } = new();
    public int ConfirmedEntries { get; set; }
    public int ResultsCaptured { get; set; }
}

public class DashboardViewModel
{
    public string OrganiserName { get; set; } = string.Empty;
    public List<DashboardEventRow> Events { get; set; } = new();

    public int TotalEvents => Events.Count;
    public int UpcomingEvents => Events.Count(r =>
        r.Event.Status is EventStatuses.Upcoming or EventStatuses.Open or EventStatuses.Closed);
    public int TotalEntries => Events.Sum(r => r.ConfirmedEntries);
    public int TotalResults => Events.Sum(r => r.ResultsCaptured);
}

// Upload form for an event banner.
public class BannerViewModel
{
    public EventDetail Event { get; set; } = new();

    [Required(ErrorMessage = "Choose an image to upload.")]
    public IFormFile? Image { get; set; }
}

// The category list and add form for one event.
public class CategoriesViewModel
{
    public EventDetail Event { get; set; } = new();
    public CategoryFormViewModel NewCategory { get; set; } = new();
}

public class CategoryFormViewModel
{
    public int? CategoryId { get; set; }
    public int EventId { get; set; }

    [Required, MaxLength(80), Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [Required, Display(Name = "Category type")]
    public string CategoryType { get; set; } = CategoryTypes.Distance;

    [Range(0.01, 9999), Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Range(0, 120), Display(Name = "Minimum age")]
    public int MinAge { get; set; }

    [Range(0, 120), Display(Name = "Maximum age")]
    public int? MaxAge { get; set; }

    [Range(0, 100000), Display(Name = "Entry fee (R)")]
    public decimal EntryFee { get; set; }

    [Range(1, 100000), Display(Name = "Maximum entries")]
    public int MaxParticipants { get; set; } = 100;

    [Display(Name = "Start time")]
    public TimeOnly? StartTime { get; set; }

    public string EventName { get; set; } = string.Empty;

    public CategoryRequest ToRequest() => new()
    {
        CategoryName = CategoryName,
        CategoryType = CategoryType,
        DistanceKm = CategoryType == CategoryTypes.Distance ? DistanceKm : null,
        MinAge = MinAge,
        MaxAge = MaxAge,
        EntryFee = EntryFee,
        MaxParticipants = MaxParticipants,
        StartTime = StartTime
    };

    public static CategoryFormViewModel From(Category c, string eventName) => new()
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
        EventName = eventName
    };
}

// The entry list and result capture page for one event.
public class EntriesViewModel
{
    public EventDetail Event { get; set; } = new();
    public List<EnrolmentDetail> Entries { get; set; } = new();
    public Dictionary<int, RaceResult> ResultsByEnrolment { get; set; } = new();

    public int ConfirmedCount => Entries.Count(e => e.Status == EnrolmentStatuses.Confirmed);
}
