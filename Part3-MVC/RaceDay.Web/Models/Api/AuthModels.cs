namespace RaceDay.Web.Models.Api;

// These classes mirror the JSON that the Part 2 API sends and receives.
// The MVC app never touches the database; everything goes through the API.

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

// Sent to POST /api/auth/login.
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// Sent to POST /api/auth/register.
public class RegisterRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Participant;
    public string? OrganiserInviteCode { get; set; }
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? City { get; set; }
}

// Returned by login, register and GET /api/users/me.
public class UserProfile
{
    public int UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? City { get; set; }
    public DateTime CreatedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}

// Sent to PUT /api/users/me.
public class UpdateProfileRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? City { get; set; }
}

// Sent to PUT /api/users/me/password.
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
