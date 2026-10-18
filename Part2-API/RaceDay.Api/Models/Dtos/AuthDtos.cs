using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models.Dtos;

// Details supplied when creating a new account.
public class RegisterRequest
{
    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    // Either "Organiser" or "Participant".
    [Required]
    public string Role { get; set; } = RoleNames.Participant;

    // Required only when registering as an Organiser. Organiser accounts are
    // controlled, so that anyone cannot simply sign up and start managing events.
    public string? OrganiserInviteCode { get; set; }

    [Phone, MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    // "Male", "Female" or "Other".
    public string? Gender { get; set; }

    [MaxLength(60)]
    public string? City { get; set; }
}

// Credentials supplied when logging in.
public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

// A user's own profile. The password hash is never returned.
public class UserProfileResponse
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
}

// Personal details a user may change on their own profile.
public class UpdateProfileRequest
{
    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Phone, MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }

    [MaxLength(60)]
    public string? City { get; set; }
}

// Used to change a password, after confirming the current one.
public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string NewPassword { get; set; } = string.Empty;
}