using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models.Dtos;

namespace RaceDay.Api.Controllers;

// Live weather for an event's venue, from the free Open-Meteo service
// (no API key needed). Uses the event's latitude and longitude.
[ApiController]
[Produces("application/json")]
public class EventWeatherController : ControllerBase
{
    public const string HttpClientName = "OpenMeteo";

    // Open-Meteo forecasts reach 16 days ahead (today plus 15).
    private const int ForecastDays = 15;

    private readonly RaceDayDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EventWeatherController> _logger;

    public EventWeatherController(RaceDayDbContext db, IHttpClientFactory httpClientFactory,
        ILogger<EventWeatherController> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // Returns the forecast for race day at the venue. For an event more than
    // 16 days away, or already past, returns today's weather at the venue
    // instead, with IsRaceDayForecast set to false. Open to everyone.
    // 404 if the event does not exist or has no coordinates; 503 if the
    // weather service cannot be reached.
    [HttpGet("api/events/{id:int}/weather")]
    [ProducesResponseType(typeof(WeatherResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WeatherResponse>> GetWeather(int id)
    {
        var raceEvent = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (raceEvent is null)
        {
            return NotFound(new { message = $"No event with id {id}." });
        }

        if (raceEvent.Latitude is null || raceEvent.Longitude is null)
        {
            return NotFound(new { message = "This event has no venue coordinates, so no weather is available." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2)); // South African time
        var isRaceDayForecast = raceEvent.EventDate >= today &&
                                raceEvent.EventDate <= today.AddDays(ForecastDays);
        var date = isRaceDayForecast ? raceEvent.EventDate : today;

        // Invariant culture, so coordinates are always written with a "." decimal
        // point, even on a machine set to South African number formatting.
        var latitude = raceEvent.Latitude.Value.ToString(CultureInfo.InvariantCulture);
        var longitude = raceEvent.Longitude.Value.ToString(CultureInfo.InvariantCulture);
        var day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var url = $"v1/forecast?latitude={latitude}&longitude={longitude}" +
                  "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,wind_speed_10m_max" +
                  $"&timezone=Africa%2FJohannesburg&start_date={day}&end_date={day}";

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            await using var body = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(body);
            var daily = json.RootElement.GetProperty("daily");

            return Ok(new WeatherResponse
            {
                EventId = id,
                Date = date,
                IsRaceDayForecast = isRaceDayForecast,
                Temperature = ReadDecimal(daily, "temperature_2m_max"),
                MinTemperature = ReadDecimal(daily, "temperature_2m_min"),
                WindSpeed = ReadDecimal(daily, "wind_speed_10m_max"),
                RainChance = (int)ReadDecimal(daily, "precipitation_probability_max"),
                Conditions = Describe((int)ReadDecimal(daily, "weather_code"))
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                       or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Weather lookup failed for event {EventId}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "The weather service is unavailable right now. Try again later." });
        }
    }

    // Open-Meteo returns each daily value as a one-item array, e.g. [24.3].
    // A missing value comes back as null and is treated as 0.
    private static decimal ReadDecimal(JsonElement daily, string name)
    {
        var value = daily.GetProperty(name)[0];
        return value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : 0m;
    }

    // WMO weather codes used by Open-Meteo, in plain words.
    internal static string Describe(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Fog",
        51 or 53 or 55 => "Drizzle",
        56 or 57 => "Freezing drizzle",
        61 => "Light rain",
        63 => "Rain",
        65 => "Heavy rain",
        66 or 67 => "Freezing rain",
        71 or 73 or 75 or 77 => "Snow",
        80 or 81 => "Rain showers",
        82 => "Heavy showers",
        85 or 86 => "Snow showers",
        95 => "Thunderstorms",
        96 or 99 => "Thunderstorms with hail",
        _ => "Unknown"
    };
}
