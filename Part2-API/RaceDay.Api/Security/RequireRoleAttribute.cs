using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Security;

// Protects an endpoint. With no roles listed, any logged-in user may call it.
// With roles listed, only those roles may.
// Returns 401 when nobody is logged in and 403 when the role is wrong.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _allowedRoles;

    public RequireRoleAttribute(params string[] allowedRoles) => _allowedRoles = allowedRoles;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;

        if (session.GetUserId() is null)
        {
            context.Result = new ObjectResult(new
            {
                message = "You must be logged in to use this endpoint."
            })
            { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        if (_allowedRoles.Length == 0)
        {
            return; // Any logged-in user is allowed.
        }

        var role = session.GetRole();
        if (role is null || !_allowedRoles.Contains(role, StringComparer.Ordinal))
        {
            context.Result = new ObjectResult(new
            {
                message = $"This endpoint is restricted to the {string.Join(" or ", _allowedRoles)} role."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}