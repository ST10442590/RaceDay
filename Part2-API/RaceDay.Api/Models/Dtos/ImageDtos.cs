namespace RaceDay.Api.Models.Dtos;

// The multipart/form-data body for uploading an event banner.
// The image goes in a form field named "File".
public class EventImageUploadRequest
{
    public IFormFile? File { get; set; }
}