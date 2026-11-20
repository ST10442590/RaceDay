using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required, MaxLength(50), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8, ErrorMessage = "Use at least 8 characters."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required, Display(Name = "I am registering as")]
    public string Role { get; set; } = Roles.Participant;

    [Display(Name = "Organiser invite code")]
    public string? OrganiserInviteCode { get; set; }

    [Phone, MaxLength(20), Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    // Needed to check age categories when entering events.
    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    [MaxLength(60)]
    public string? City { get; set; }
}

public class ProfileViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime MemberSince { get; set; }

    [Required, MaxLength(50), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Phone, MaxLength(20), Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    [MaxLength(60)]
    public string? City { get; set; }
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8, ErrorMessage = "Use at least 8 characters."), DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
