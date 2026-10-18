namespace RaceDay.Api.Models;

// Lookup table of the two system roles: Organiser and Participant.
public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}

// Fixed role identifiers, matching the seeded Roles table.
public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";

    public const int OrganiserId = 1;
    public const int ParticipantId = 2;
}
