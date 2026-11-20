using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

// Pages for logged-in Participants: their entries and their results.
[RoleRequired(Roles.Participant)]
public class ParticipantController : RaceDayControllerBase
{
    public ParticipantController(ApiClient api) : base(api)
    {
    }

    public async Task<IActionResult> MyEvents()
    {
        var enrolments = await Api.GetAsync<List<Enrolment>>("api/enrolments/me");
        if (!enrolments.Success)
        {
            return ApiError(enrolments);
        }

        var results = await Api.GetAsync<List<RaceResult>>("api/results/me");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var all = enrolments.Data ?? new List<Enrolment>();

        return View(new MyEventsViewModel
        {
            Upcoming = all.Where(e => e.EventDate >= today).OrderBy(e => e.EventDate).ToList(),
            Past = all.Where(e => e.EventDate < today).OrderByDescending(e => e.EventDate).ToList(),
            EnrolmentsWithResults = (results.Data ?? new List<RaceResult>())
                .Select(r => r.EnrolmentId)
                .ToHashSet()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await Api.SendWithoutResultAsync(HttpMethod.Delete, $"api/enrolments/{id}");

        var authFailure = HandleAuthFailure(result);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (result.Success)
        {
            Flash("Your entry has been cancelled.", "info");
        }
        else
        {
            Flash(result.Error ?? "Your entry could not be cancelled.", "danger");
        }

        return RedirectToAction(nameof(MyEvents));
    }

    public async Task<IActionResult> MyResults()
    {
        var results = await Api.GetAsync<List<RaceResult>>("api/results/me");
        if (!results.Success)
        {
            return ApiError(results);
        }

        var mine = results.Data ?? new List<RaceResult>();
        var rows = new List<MyResultRow>();

        // For "47th of 312", count the finishers in the same category from
        // each event's public results board. One call per event is enough.
        foreach (var eventGroup in mine.GroupBy(r => r.EventId))
        {
            var board = await Api.GetAsync<List<RaceResult>>($"api/events/{eventGroup.Key}/results");
            var raceEvent = await Api.GetAsync<EventDetail>($"api/events/{eventGroup.Key}");
            var boardResults = board.Data ?? new List<RaceResult>();

            foreach (var result in eventGroup)
            {
                rows.Add(new MyResultRow
                {
                    Result = result,
                    EventDate = raceEvent.Data?.EventDate ?? default,
                    // Never show "5th of 3" if results are still being captured.
                    FinishersInCategory = Math.Max(
                        result.FinishPosition ?? 0,
                        boardResults.Count(r =>
                            r.CategoryId == result.CategoryId &&
                            r.ResultStatus == ResultStatuses.Finished))
                });
            }
        }

        return View(rows.OrderByDescending(r => r.EventDate).ToList());
    }
}