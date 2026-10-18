namespace RaceDay.Api.Models;

// Records that a Participant has entered an Event in a chosen Category.
// This resolves the many-to-many relationship between participants and
// categories, and stores the link between all three as the brief requires.
public class Enrolment
{
    public int EnrolmentId { get; set; }

    public int ParticipantId { get; set; }
    public int EventId { get; set; }
    public int CategoryId { get; set; }

    public string? RaceNumber { get; set; }
    public string Status { get; set; } = EnrolmentStatuses.Confirmed;
    public DateTime EnrolledAt { get; set; }

    public User? Participant { get; set; }
    public EventCategory? Category { get; set; }
    public Result? Result { get; set; }
}

public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Pending, Confirmed, Cancelled };
}
