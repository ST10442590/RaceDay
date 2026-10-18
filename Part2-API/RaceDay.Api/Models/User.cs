namespace RaceDay.Api.Models;

// An account on the system. Every user holds exactly one role,
// which decides what they are allowed to do.
public class User
{
    public int UserId { get; set; }
    public int RoleId { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Hashed password. The plain text password is never stored.
    public string PasswordHash { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? City { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public Role? Role { get; set; }
    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
    public ICollection<Result> RecordedResults { get; set; } = new List<Result>();
}
