using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models;

// Small display helpers shared by the views, so colours and icons are
// consistent on every page.
public static class UiHelpers
{
    // Colour-coded event statuses.
    public static string EventStatusClass(string status) => status switch
    {
        EventStatuses.Open => "status-open",
        EventStatuses.Upcoming => "status-upcoming",
        EventStatuses.Closed => "status-closed",
        EventStatuses.Completed => "status-completed",
        EventStatuses.Cancelled => "status-cancelled",
        _ => "status-upcoming"
    };

    public static string EventStatusLabel(string status) => status switch
    {
        EventStatuses.Open => "Entries open",
        EventStatuses.Upcoming => "Coming soon",
        EventStatuses.Closed => "Entries closed",
        EventStatuses.Completed => "Completed",
        EventStatuses.Cancelled => "Cancelled",
        _ => status
    };

    // Colour-coded enrolment statuses.
    public static string EnrolmentStatusClass(string status) => status switch
    {
        EnrolmentStatuses.Confirmed => "status-open",
        EnrolmentStatuses.Pending => "status-closed",
        EnrolmentStatuses.Cancelled => "status-cancelled",
        _ => "status-upcoming"
    };

    // Colour-coded result statuses.
    public static string ResultStatusClass(string status) => status switch
    {
        ResultStatuses.Finished => "status-open",
        ResultStatuses.DidNotFinish => "status-closed",
        ResultStatuses.DidNotStart => "status-upcoming",
        ResultStatuses.Disqualified => "status-cancelled",
        _ => "status-upcoming"
    };

    // Bootstrap Icons name for each event type.
    public static string EventTypeIcon(string type) => type switch
    {
        "Cycle" => "bi-bicycle",
        "Walk" => "bi-person-walking",
        _ => "bi-lightning-charge-fill"
    };

    // Banner gradient used when an event has no uploaded image yet.
    public static string EventTypeClass(string type) => type switch
    {
        "Cycle" => "type-cycle",
        "Walk" => "type-walk",
        _ => "type-run"
    };

    // Bootstrap Icons name for a weather description from the API.
    public static string WeatherIcon(string conditions) => conditions switch
    {
        "Clear sky" or "Mainly clear" => "bi-sun",
        "Partly cloudy" => "bi-cloud-sun",
        "Overcast" => "bi-clouds",
        "Fog" => "bi-cloud-fog2",
        var c when c.Contains("Thunder") => "bi-cloud-lightning-rain",
        var c when c.Contains("Snow") => "bi-snow",
        var c when c.Contains("rain", StringComparison.OrdinalIgnoreCase) ||
                   c.Contains("shower", StringComparison.OrdinalIgnoreCase) ||
                   c.Contains("drizzle", StringComparison.OrdinalIgnoreCase) => "bi-cloud-rain",
        _ => "bi-cloud"
    };

    // 1 -> "1st", 2 -> "2nd", 47 -> "47th", 112 -> "112th".
    public static string Ordinal(int number)
    {
        var lastTwo = number % 100;
        if (lastTwo is 11 or 12 or 13)
        {
            return $"{number}th";
        }

        return (number % 10) switch
        {
            1 => $"{number}st",
            2 => $"{number}nd",
            3 => $"{number}rd",
            _ => $"{number}th"
        };
    }

    public static string Distance(decimal km) => $"{km:0.##} km";

    public static string Money(decimal amount) => amount == 0 ? "Free" : $"R{amount:0.00}";

    public static string Time(TimeOnly? time) => time is null ? "–" : time.Value.ToString("HH:mm");

    public static string FinishTime(TimeOnly? time) => time is null ? "–" : time.Value.ToString("H:mm:ss");

    public static string Date(DateOnly date) => date.ToString("ddd d MMM yyyy");
}
