using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

// Lets an Organiser add, edit and remove the categories of their own events.
public class CategoriesController : OrganiserControllerBase
{
    public CategoriesController(ApiClient api) : base(api)
    {
    }

    public async Task<IActionResult> Index(int eventId)
    {
        var (raceEvent, failure) = await LoadOwnEventAsync(eventId);
        if (failure is not null)
        {
            return failure;
        }

        return View(new CategoriesViewModel
        {
            Event = raceEvent!,
            NewCategory = new CategoryFormViewModel { EventId = eventId, EventName = raceEvent!.Name }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int eventId, [Bind(Prefix = "NewCategory")] CategoryFormViewModel model)
    {
        var (raceEvent, failure) = await LoadOwnEventAsync(eventId);
        if (failure is not null)
        {
            return failure;
        }

        ValidateCategory(model, "NewCategory.");

        if (ModelState.IsValid)
        {
            var result = await Api.PostAsync<Category>($"api/events/{eventId}/categories", model.ToRequest());

            var authFailure = HandleAuthFailure(result);
            if (authFailure is not null)
            {
                return authFailure;
            }

            if (result.Success)
            {
                Flash($"Category \"{model.CategoryName}\" added.");
                return RedirectToAction(nameof(Index), new { eventId });
            }

            ModelState.AddModelError(string.Empty, result.Error ?? "The category could not be added.");
        }

        model.EventId = eventId;
        model.EventName = raceEvent!.Name;
        return View(nameof(Index), new CategoriesViewModel { Event = raceEvent, NewCategory = model });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await Api.GetAsync<Category>($"api/categories/{id}");
        if (!category.Success || category.Data is null)
        {
            return ApiError(category);
        }

        var (raceEvent, failure) = await LoadOwnEventAsync(category.Data.EventId);
        if (failure is not null)
        {
            return failure;
        }

        return View(CategoryFormViewModel.From(category.Data, raceEvent!.Name));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategoryFormViewModel model)
    {
        model.CategoryId = id;
        ValidateCategory(model, string.Empty);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await Api.PutAsync<Category>($"api/categories/{id}", model.ToRequest());

        var authFailure = HandleAuthFailure(result);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The category could not be saved.");
            return View(model);
        }

        Flash("Category saved.");
        return RedirectToAction(nameof(Index), new { eventId = model.EventId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int eventId)
    {
        var result = await Api.SendWithoutResultAsync(HttpMethod.Delete, $"api/categories/{id}");

        var authFailure = HandleAuthFailure(result);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (result.Success)
        {
            Flash("Category removed.", "info");
        }
        else
        {
            Flash(result.Error ?? "The category could not be removed.", "danger");
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }

    // The same rules the API applies, checked first for a quicker message.
    private void ValidateCategory(CategoryFormViewModel model, string prefix)
    {
        if (model.CategoryType == CategoryTypes.Distance && model.DistanceKm is null)
        {
            ModelState.AddModelError(prefix + nameof(model.DistanceKm), "A distance category needs a distance.");
        }

        if (model.MaxAge is not null && model.MaxAge < model.MinAge)
        {
            ModelState.AddModelError(prefix + nameof(model.MaxAge), "The maximum age cannot be below the minimum age.");
        }
    }
}