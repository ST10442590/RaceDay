using RaceDay.Api.Models;

namespace RaceDay.Api.Security;

// Keys under which values are stored in the server-side session.
public static class SessionKeys
{
    public const string UserId = "RaceDay.UserId";
    public const string Role = "RaceDay.Role";
    public const string DisplayName = "RaceDay.DisplayName";
}

// Helpers for reading and writing the logged-in user's identity.
// The session itself is held on the server; the browser only ever
// receives an opaque session cookie.
public static class SessionExtensions
{
    // Records the user's id and role in the session after a successful login.
    public static void SignIn(this ISession session, User user, string roleName)
    {
        session.SetInt32(SessionKeys.UserId, user.UserId);
        session.SetString(SessionKeys.Role, roleName);
        session.SetString(SessionKeys.DisplayName, $"{user.FirstName} {user.LastName}");
    }

    // The logged-in user's id, or null if nobody is logged in.
    public static int? GetUserId(this ISession session) => session.GetInt32(SessionKeys.UserId);

    // The logged-in user's role, or null if nobody is logged in.
    public static string? GetRole(this ISession session) => session.GetString(SessionKeys.Role);

    public static string? GetDisplayName(this ISession session) => session.GetString(SessionKeys.DisplayName);

    public static bool IsInRole(this ISession session, string role) =>
        string.Equals(session.GetRole(), role, StringComparison.Ordinal);
}