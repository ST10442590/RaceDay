using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Models.Dtos;
using Xunit;

namespace RaceDay.Api.Tests;

// Covers the live weather endpoint. The Open-Meteo service is replaced with
// a fake HTTP handler, so the tests never go to the internet.
public class EventWeatherControllerTests
{
    private const string SampleForecast = """
        {
          "daily": {
            "time": ["2026-12-13"],
            "weather_code": [63],
            "temperature_2m_max": [24.3],
            "temperature_2m_min": [13.1],
            "precipitation_probability_max": [70],
            "wind_speed_10m_max": [18.4]
          }
        }
        """;

    private static EventWeatherController CreateController(RaceDayDbContext db, HttpMessageHandler handler) =>
        new(db, new FakeHttpClientFactory(handler), NullLogger<EventWeatherController>.Instance);

    [Fact]
    public async Task GetWeather_ForAnEventWithCoordinates_ReturnsTheForecast()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var raceEvent = TestDatabase.TestEvent(db);
        raceEvent.Latitude = -25.7479m;
        raceEvent.Longitude = 28.2293m;
        db.SaveChanges();

        var handler = new FakeHandler(HttpStatusCode.OK, SampleForecast);
        var controller = CreateController(db, handler);

        var result = await controller.GetWeather(raceEvent.EventId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var weather = Assert.IsType<WeatherResponse>(ok.Value);
        Assert.Equal(24.3m, weather.Temperature);
        Assert.Equal("Rain", weather.Conditions);
        Assert.Equal(70, weather.RainChance);

        // Coordinates are sent with a "." decimal point whatever the machine's culture.
        Assert.Contains("latitude=-25.7479", handler.LastRequestUrl);
    }

    [Fact]
    public async Task GetWeather_ForAnEventWithoutCoordinates_Returns404()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);

        var controller = CreateController(db, new FakeHandler(HttpStatusCode.OK, SampleForecast));

        var result = await controller.GetWeather(TestDatabase.TestEvent(db).EventId);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetWeather_WhenTheWeatherServiceFails_Returns503()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var raceEvent = TestDatabase.TestEvent(db);
        raceEvent.Latitude = -26.2041m;
        raceEvent.Longitude = 28.0473m;
        db.SaveChanges();

        var controller = CreateController(db, new FakeHandler(HttpStatusCode.InternalServerError, "{}"));

        var result = await controller.GetWeather(raceEvent.EventId);

        var unavailable = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
    }
}

// Returns a fixed response and remembers the URL that was requested.
public class FakeHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;

    public FakeHandler(HttpStatusCode status, string body)
    {
        _status = status;
        _body = body;
    }

    public string LastRequestUrl { get; private set; } = string.Empty;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUrl = request.RequestUri?.ToString() ?? string.Empty;
        return Task.FromResult(new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json")
        });
    }
}

public class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) =>
        new(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.open-meteo.com/") };
}
