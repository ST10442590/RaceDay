using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

// Shared helpers for every RaceDay controller.
public abstract class RaceDayControllerBase : Controller
{
    protected RaceDayControllerBase(ApiClient api) => Api = api;

    protected ApiClient Api { get; }

    protected int? CurrentUserId => HttpContext.Session.GetUserId();

    // Shows a coloured message at the top of the next page.
    // Type is a Bootstrap colour: success, danger, warning or info.
    protected void Flash(string message, string type = "success")
    {
        TempData["FlashMessage"] = message;
        TempData["FlashType"] = type;
    }

    // Deals with the two API failures that need the same handling on every
    // page. Returns null when the caller should handle the error itself.
    //   401: the API no longer recognises the user (it may have been
    //        restarted), so log out of the MVC app too and ask them to log in.
    //   403: the API refused the action for this user.
    protected IActionResult? HandleAuthFailure(ApiResult result)
    {
        if (result.IsUnauthorised)
        {
            HttpContext.Session.SignOut();
            Flash("Your session has expired. Please log in again.", "warning");
            var returnUrl = Request.Path + Request.QueryString;
            return RedirectToAction("Login", "Account", new { returnUrl });
        }

        if (result.IsForbidden)
        {
            Flash(result.Error ?? "You are not allowed to do that.", "danger");
            return RedirectToAction("AccessDenied", "Account");
        }

        return null;
    }

    // A friendly page for when an API call fails outright.
    protected IActionResult ApiError(ApiResult result)
    {
        var handled = HandleAuthFailure(result);
        if (handled is not null)
        {
            return handled;
        }

        if (result.IsNotFound)
        {
            return NotFound();
        }

        ViewBag.ErrorMessage = result.Error;
        return View("ApiError");
    }
}
