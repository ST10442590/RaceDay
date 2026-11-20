using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Web.Security;

// Protects an MVC page. Visitors who are not logged in are sent to the login
// page (and brought back afterwards); users with the wrong role are sent to
// the access denied page. With no roles listed, any logged-in user may enter.
// The API enforces the same rules again, so this is about a clean experience,
// not the only line of defence.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RoleRequiredAttribute : ActionFilterAttribute
{
    private readonly string[] _roles;

    public RoleRequiredAttribute(params string[] roles) => _roles = roles;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;

        if (!session.IsLoggedIn())
        {
            var request = context.HttpContext.Request;
            var returnUrl = request.Path + request.QueryString;

            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(session.GetRole()))
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}
