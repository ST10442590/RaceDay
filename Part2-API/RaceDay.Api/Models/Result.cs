namespace RaceDay.Api.Models;

// A participant's finish time and finishing position for one enrolment,
// captured by the Organiser after the event.
public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }

    // Set only when the participant finished.
    public TimeOnly? FinishTime { get; set; }

    // Position within the category. Set only when the participant finished.
    public int? FinishPosition { get; set; }

    public string ResultStatus { get; set; } = ResultStatuses.Finished;

    // The Organiser who captured this result.
    public int RecordedById { get; set; }
    public DateTime RecordedAt { get; set; }

    public Enrolment? Enrolment { get; set; }
    public User? RecordedBy { get; set; }
}

public static class ResultStatuses
{
    public const string Finished = "Finished";
    public const string DidNotFinish = "DNF";
    public const string DidNotStart = "DNS";
    public const string Disqualified = "DQ";

    public static readonly string[] All =
        { Finished, DidNotFinish, DidNotStart, Disqualified };
}
