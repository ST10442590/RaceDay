using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models.Dtos;

// Sent by a Participant entering an event. Both ids are needed: the system
// records the link between the Participant, the event and the category.
public class CreateEnrolmentRequest
{
    [Required]
    public int EventId { get; set; }

    [Required]
    public int CategoryId { get; set; }
}

// An enrolment as the Participant sees it on their own "My events" list.
public class EnrolmentResponse
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

// An enrolment as the Organiser sees it on their event's entry list.
public class EnrolmentDetailResponse : EnrolmentResponse
{
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string ParticipantEmail { get; set; } = string.Empty;
    public bool HasResult { get; set; }
}

// Sent by an Organiser capturing a result after the event.
public class CreateResultRequest
{
    [Required]
    public int EnrolmentId { get; set; }

    // Required when ResultStatus is Finished; must be null otherwise.
    public TimeOnly? FinishTime { get; set; }

    // Position within the category. Required when Finished, null otherwise.
    [Range(1, 100000)]
    public int? FinishPosition { get; set; }

    // Finished, DNF, DNS or DQ.
    [Required]
    public string ResultStatus { get; set; } = ResultStatuses.Finished;
}

// Sent by an Organiser correcting a result.
public class UpdateResultRequest
{
    public TimeOnly? FinishTime { get; set; }

    [Range(1, 100000)]
    public int? FinishPosition { get; set; }

    [Required]
    public string ResultStatus { get; set; } = ResultStatuses.Finished;
}

// A captured result.
public class ResultResponse
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