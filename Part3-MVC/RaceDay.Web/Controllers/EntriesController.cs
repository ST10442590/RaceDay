using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

// Lets an Organiser see who has entered their event and capture results.
public class EntriesController : OrganiserControllerBase
{
    public EntriesController(ApiClient api) : base(api)
    {
    }

    public async Task<IActionResult> Index(int eventId)
    {
        var (raceEvent, failure) = await LoadOwnEventAsync(eventId);
        if (failure is not null)
        {
            return failure;
        }

        var entries = await Api.GetAsync<List<EnrolmentDetail>>($"api/events/{eventId}/enrolments");
        if (!entries.Success)
        {
            return ApiError(entries);
        }

        var results = await Api.GetAsync<List<RaceResult>>($"api/events/{eventId}/results");

        return View(new EntriesViewModel
        {
            Event = raceEvent!,
            Entries = (entries.Data ?? new List<EnrolmentDetail>())
                .OrderBy(e => e.CategoryName)
                .ThenBy(e => e.RaceNumber)
                .ToList(),
            ResultsByEnrolment = (results.Data ?? new List<RaceResult>())
                .ToDictionary(r => r.EnrolmentId)
        });
    }

    // Captures a new result, or corrects an existing one.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveResult(int eventId, int enrolmentId, int? resultId,
        string resultStatus, TimeOnly? finishTime, int? finishPosition)
    {
        // A non-finisher has no time or position.
        if (resultStatus != ResultStatuses.Finished)
        {
            finishTime = null;
            finishPosition = null;
        }

        var request = new ResultRequest
        {
            EnrolmentId = enrolmentId,
            ResultStatus = resultStatus,
            FinishTime = finishTime,
            FinishPosition = finishPosition
        };

        var result = resultId is null
            ? await Api.PostAsync<RaceResult>("api/results", request)
            : await Api.PutAsync<RaceResult>($"api/results/{resultId}", request);

        var authFailure = HandleAuthFailure(result);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (result.Success && result.Data is not null)
        {
            Flash($"Result saved for {result.Data.ParticipantName}.");
        }
        else
        {
            Flash(result.Error ?? "The result could not be saved.", "danger");
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteResult(int eventId, int resultId)
    {
        var result = await Api.SendWithoutResultAsync(HttpMethod.Delete, $"api/results/{resultId}");

        var authFailure = HandleAuthFailure(result);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (result.Success)
        {
            Flash("Result removed.", "info");
        }
        else
        {
            Flash(result.Error ?? "The result could not be removed.", "danger");
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }
}