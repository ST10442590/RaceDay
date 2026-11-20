using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Security;

// Keys for the values the MVC app keeps in its own server-side session.
public static class UserSessionKeys
{
    public const string UserId = "Web.UserId";
    public const string Role = "Web.Role";
    public const string Name = "Web.Name";

    // The API's session cookie ("RaceDay.Session=..."). The API identifies
    // the logged-in user by this cookie, so the MVC app stores it after
    // login and sends it back on every API call made for this user.
    public const string ApiCookie = "Web.ApiCookie";
}

// Helpers for reading and writing the logged-in user in the MVC session.
public static class UserSession
{
    public static void SignIn(this ISession session, UserProfile profile)
    {
        session.SetInt32(UserSessionKeys.UserId, profile.UserId);
        session.SetString(UserSessionKeys.Role, profile.Role);
        session.SetString(UserSessionKeys.Name, profile.FirstName);
    }

    public static void SignOut(this ISession session) => session.Clear();

    public static int? GetUserId(this ISession session) => session.GetInt32(UserSessionKeys.UserId);

    public static string? GetRole(this ISession session) => session.GetString(UserSessionKeys.Role);

    public static string? GetName(this ISession session) => session.GetString(UserSessionKeys.Name);

    public static bool IsLoggedIn(this ISession session) => session.GetUserId() is not null;

    public static bool IsOrganiser(this ISession session) => session.GetRole() == Roles.Organiser;

    public static bool IsParticipant(this ISession session) => session.GetRole() == Roles.Participant;

    public static string? GetApiCookie(this ISession session) => session.GetString(UserSessionKeys.ApiCookie);

    public static void SetApiCookie(this ISession session, string cookie) =>
        session.SetString(UserSessionKeys.ApiCookie, cookie);
}
