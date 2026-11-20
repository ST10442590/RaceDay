using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

// Shared by every Organiser page. Only Organisers may enter, and an
// Organiser may only manage events they created.
[RoleRequired(Roles.Organiser)]
public abstract class OrganiserControllerBase : RaceDayControllerBase
{
    protected OrganiserControllerBase(ApiClient api) : base(api)
    {
    }

    // Loads an event and confirms the logged-in Organiser owns it. Returns
    // either the event, or the page to show instead (not found, access denied).
    protected async Task<(EventDetail? Event, IActionResult? Failure)> LoadOwnEventAsync(int eventId)
    {
        var result = await Api.GetAsync<EventDetail>($"api/events/{eventId}");
        if (!result.Success || result.Data is null)
        {
            return (null, ApiError(result));
        }

        if (result.Data.OrganiserId != CurrentUserId)
        {
            Flash("You can only manage events that you created.", "danger");
            return (null, RedirectToAction("AccessDenied", "Account"));
        }

        return (result.Data, null);
    }
}