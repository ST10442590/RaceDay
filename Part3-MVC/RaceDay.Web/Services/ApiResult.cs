using System.Net;

namespace RaceDay.Web.Services;

// The outcome of one call to the API: whether it worked, the HTTP status,
// and either the data or a readable error message taken from the API.
public class ApiResult
{
    public bool Success { get; init; }
    public HttpStatusCode StatusCode { get; init; }
    public string? Error { get; init; }

    // 401 from the API: nobody is logged in there (for example the API was
    // restarted and its in-memory sessions were cleared).
    public bool IsUnauthorised => StatusCode == HttpStatusCode.Unauthorized;

    // 403 from the API: logged in, but not allowed to do this.
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;

    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
}

public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }
}
