namespace RaceDay.Api.Models.Dtos;

// Weather at an event's venue, from GET /api/events/{id}/weather.
public class WeatherResponse
{
    public int EventId { get; set; }

    // The day this weather is for.
    public DateOnly Date { get; set; }

    // True when Date is race day. Forecasts only reach 16 days ahead, so
    // for events further away (or already past) today's weather is returned.
    public bool IsRaceDayForecast { get; set; }

    public decimal Temperature { get; set; }
    public decimal MinTemperature { get; set; }
    public string Conditions { get; set; } = string.Empty;
    public decimal WindSpeed { get; set; }
    public int RainChance { get; set; }

    public string Source { get; set; } = "Open-Meteo";
}