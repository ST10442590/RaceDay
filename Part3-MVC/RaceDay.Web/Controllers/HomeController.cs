using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

// The home page: browse and filter events. Open to everyone.
public class HomeController : RaceDayControllerBase
{
    public HomeController(ApiClient api) : base(api)
    {
    }

    public async Task<IActionResult> Index(string? type, string? province, string? search, bool includePast = false)
    {
        var model = new HomeViewModel
        {
            Type = type,
            Province = province,
            Search = search,
            IncludePast = includePast
        };

        // The API filters by type and province; the name search is done here.
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(type))
        {
            query.Add($"type={Uri.EscapeDataString(type)}");
        }

        if (!string.IsNullOrWhiteSpace(province))
        {
            query.Add($"province={Uri.EscapeDataString(province)}");
        }

        if (!includePast)
        {
            query.Add($"from={DateTime.Today:yyyy-MM-dd}");
        }

        var path = "api/events" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);

        var events = await Api.GetAsync<List<EventSummary>>(path);
        var types = await Api.GetAsync<List<EventTypeItem>>("api/event-types");

        if (!events.Success)
        {
            model.ErrorMessage = events.Error;
            return View(model);
        }

        var list = events.Data ?? new List<EventSummary>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            list = list.Where(e =>
                    e.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    e.City.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    e.VenueName.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        model.Events = list;
        model.EventTypes = types.Data ?? new List<EventTypeItem>();
        return View(model);
    }

    // Shown for 404s and other status codes without their own page.
    public IActionResult NotFoundPage(int code)
    {
        Response.StatusCode = code;
        ViewBag.Code = code;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}