namespace RaceDay.Web.Models.Api;

public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}

public static class ResultStatuses
{
    public const string Finished = "Finished";
    public const string DidNotFinish = "DNF";
    public const string DidNotStart = "DNS";
    public const string Disqualified = "DQ";

    public static readonly string[] All = { Finished, DidNotFinish, DidNotStart, Disqualified };

    public static string Describe(string status) => status switch
    {
        DidNotFinish => "Did not finish",
        DidNotStart => "Did not start",
        Disqualified => "Disqualified",
        _ => status
    };
}

// Sent to POST /api/enrolments.
public class EnrolmentRequest
{
    public int EventId { get; set; }
    public int CategoryId { get; set; }
}

// A Participant's own enrolment, from GET /api/enrolments/me.
public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? RaceNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
}

// An entry on an Organiser's entry list, from GET /api/events/{id}/enrolments.
public class EnrolmentDetail : Enrolment
{
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string ParticipantEmail { get; set; } = string.Empty;
    public bool HasResult { get; set; }
}

// A captured result.
public class RaceResult
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string? RaceNumber { get; set; }
    public TimeOnly? FinishTime { get; set; }
    public int? FinishPosition { get; set; }
    public string ResultStatus { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}

// Sent to POST /api/results and PUT /api/results/{id}.
public class ResultRequest
{
    public int EnrolmentId { get; set; }
    public TimeOnly? FinishTime { get; set; }
    public int? FinishPosition { get; set; }
    public string ResultStatus { get; set; } = ResultStatuses.Finished;
}
