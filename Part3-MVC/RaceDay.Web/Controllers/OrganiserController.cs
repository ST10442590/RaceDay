using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

// The Organiser dashboard, and creating, editing and deleting events,
// including uploading the event banner to Azure Blob Storage via the API.
public class OrganiserController : OrganiserControllerBase
{
    // Matches the limits the API enforces.
    private const long MaxBannerBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp" };

    public OrganiserController(ApiClient api) : base(api)
    {
    }

    public async Task<IActionResult> Dashboard()
    {
        var events = await Api.GetAsync<List<EventSummary>>("api/events/mine");
        if (!events.Success)
        {
            return ApiError(events);
        }

        var model = new DashboardViewModel
        {
            OrganiserName = HttpContext.Session.GetName() ?? "Organiser"
        };

        foreach (var e in events.Data ?? new List<EventSummary>())
        {
            var entries = await Api.GetAsync<List<EnrolmentDetail>>($"api/events/{e.EventId}/enrolments");
            var list = entries.Data ?? new List<EnrolmentDetail>();

            model.Events.Add(new DashboardEventRow
            {
                Event = e,
                ConfirmedEntries = list.Count(x => x.Status == EnrolmentStatuses.Confirmed),
                ResultsCaptured = list.Count(x => x.HasResult)
            });
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new EventFormViewModel();
        await LoadEventTypesAsync(model);
        return View("EventForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LoadEventTypesAsync(model);
            return View("EventForm", model);
        }

        var result = await Api.PostAsync<EventDetail>("api/events", model.ToRequest());

        var authFailure = HandleAuthFailure(result);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The event could not be created.");
            await LoadEventTypesAsync(model);
            return View("EventForm", model);
        }

        Flash($"\"{result.Data.Name}\" has been created. Now add its categories.");
        return RedirectToAction("Index", "Categories", new { eventId = result.Data.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var (raceEvent, failure) = await LoadOwnEventAsync(id);
        if (failure is not null)
        {
            return failure;
        }

        var model = EventFormViewModel.From(raceEvent!);
        await LoadEventTypesAsync(model, raceEvent!.EventType);
        return View("EventForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model)
    {
        model.EventId = id;

        if (!ModelState.IsValid)
        {
            await LoadEventTypesAsync(model);
            return View("EventForm", model);
        }

        var result = await Api.PutAsync<EventDetail>($"api/events/{id}", model.ToRequest());

        var authFailure = HandleAuthFailure(result);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The event could not be saved.");
            await LoadEventTypesAsync(model);
            return View("EventForm", model);
        }

        Flash("Event details saved.");
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await Api.SendWithoutResultAsync(HttpMethod.Delete, $"api/events/{id}");

        var authFailure = HandleAuthFailure(result);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (result.Success)
        {
            Flash("The event has been deleted.", "info");
        }
        else
        {
            // For example 409: the event already has entries.
            Flash(result.Error ?? "The event could not be deleted.", "danger");
        }

        return RedirectToAction(nameof(Dashboard));
    }

    [HttpGet]
    public async Task<IActionResult> Banner(int id)
    {
        var (raceEvent, failure) = await LoadOwnEventAsync(id);
        if (failure is not null)
        {
            return failure;
        }

        return View(new BannerViewModel { Event = raceEvent! });
    }

    // Sends the chosen image to the API, which stores it in Azure Blob
    // Storage and saves the blob's URL against the event.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxBannerBytes + 1024 * 1024)]
    public async Task<IActionResult> Banner(int id, BannerViewModel model)
    {
        var (raceEvent, failure) = await LoadOwnEventAsync(id);
        if (failure is not null)
        {
            return failure;
        }

        model.Event = raceEvent!;

        if (model.Image is not null)
        {
            if (model.Image.Length > MaxBannerBytes)
            {
                ModelState.AddModelError(nameof(model.Image), "The image must be 5 MB or smaller.");
            }
            else if (!AllowedImageTypes.Contains(model.Image.ContentType))
            {
                ModelState.AddModelError(nameof(model.Image), "Upload a JPG, PNG or WebP image.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await Api.PostFileAsync<EventDetail>($"api/events/{id}/image", model.Image!);

        var authFailure = HandleAuthFailure(result);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The image could not be uploaded.");
            return View(model);
        }

        Flash("Banner uploaded to Azure Blob Storage.");
        return RedirectToAction(nameof(Banner), new { id });
    }

    // Fills the event type drop-down. If the API cannot be reached, falls back
    // to the three seeded types so the form still works.
    private async Task LoadEventTypesAsync(EventFormViewModel model, string? currentTypeName = null)
    {
        var types = await Api.GetAsync<List<EventTypeItem>>("api/event-types");
        model.EventTypes = types.Data ?? new List<EventTypeItem>
        {
            new() { EventTypeId = 1, TypeName = "Run" },
            new() { EventTypeId = 2, TypeName = "Walk" },
            new() { EventTypeId = 3, TypeName = "Cycle" }
        };

        // The event detail returns the type name, so map it back to its id.
        if (currentTypeName is not null)
        {
            var match = model.EventTypes.FirstOrDefault(t => t.TypeName == currentTypeName);
            if (match is not null)
            {
                model.EventTypeId = match.EventTypeId;
            }
        }
    }
}