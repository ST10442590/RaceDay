using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

// Public event pages: detail, Enter Event, and the results board.
public class EventsController : RaceDayControllerBase
{
    public EventsController(ApiClient api) : base(api)
    {
    }

    public async Task<IActionResult> Details(int id)
    {
        var result = await Api.GetAsync<EventDetail>($"api/events/{id}");
        if (!result.Success || result.Data is null)
        {
            return ApiError(result);
        }

        var model = new EventDetailsViewModel { Event = result.Data };

        // A logged-in Participant sees whether they have already entered.
        if (HttpContext.Session.IsParticipant())
        {
            var mine = await Api.GetAsync<List<Enrolment>>("api/enrolments/me");
            model.MyEnrolment = mine.Data?.FirstOrDefault(e =>
                e.EventId == id && e.Status != EnrolmentStatuses.Cancelled);
        }

        // Weather is a nice extra: if the lookup fails, the page still loads.
        if (result.Data.Latitude is not null && result.Data.Longitude is not null)
        {
            var weather = await Api.GetAsync<WeatherInfo>($"api/events/{id}/weather");
            model.Weather = weather.Data;
        }

        return View(model);
    }

    // Enters the logged-in Participant into a category of this event. The API
    // checks the deadline, capacity, age limits and duplicate entries.
    [HttpPost]
    [RoleRequired(Roles.Participant)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enter(int id, int categoryId)
    {
        var result = await Api.PostAsync<Enrolment>("api/enrolments",
            new EnrolmentRequest { EventId = id, CategoryId = categoryId });

        var authFailure = HandleAuthFailure(result);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (!result.Success || result.Data is null)
        {
            Flash(result.Error ?? "Your entry could not be completed.", "danger");
            return RedirectToAction(nameof(Details), new { id });
        }

        Flash($"You're in! You are entered in {result.Data.CategoryName} with race number {result.Data.RaceNumber}.");
        return RedirectToAction("MyEvents", "Participant");
    }

    public async Task<IActionResult> Results(int id)
    {
        var raceEvent = await Api.GetAsync<EventDetail>($"api/events/{id}");
        if (!raceEvent.Success || raceEvent.Data is null)
        {
            return ApiError(raceEvent);
        }

        var results = await Api.GetAsync<List<RaceResult>>($"api/events/{id}/results");
        if (!results.Success)
        {
            return ApiError(results);
        }

        return View(new EventResultsViewModel
        {
            Event = raceEvent.Data,
            Results = results.Data ?? new List<RaceResult>()
        });
    }
}