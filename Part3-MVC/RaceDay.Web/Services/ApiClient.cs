using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Web.Security;

namespace RaceDay.Web.Services;

// The only way the MVC app talks to the RaceDay API.
//
// How login works across the two apps:
//   1. The user logs in on the MVC site. The MVC app posts the email and
//      password to the API's /api/auth/login.
//   2. The API starts its own session and replies with a "RaceDay.Session"
//      cookie. Browsers never see that cookie - the MVC app keeps it in the
//      user's MVC session.
//   3. On every later API call for that user, the MVC app sends the cookie
//      back, so the API knows who is calling and applies its role checks.
public class ApiClient
{
    public const string ApiSessionCookieName = "RaceDay.Session";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor, ILogger<ApiClient> logger)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private ISession? Session => _httpContextAccessor.HttpContext?.Session;

    // ------------------------------------------------------------------
    // Typed helpers used by the controllers
    // ------------------------------------------------------------------

    public Task<ApiResult<T>> GetAsync<T>(string path) =>
        SendAsync<T>(HttpMethod.Get, path, null);

    public Task<ApiResult<T>> PostAsync<T>(string path, object body) =>
        SendAsync<T>(HttpMethod.Post, path, JsonContent.Create(body, options: Json));

    public Task<ApiResult<T>> PutAsync<T>(string path, object body) =>
        SendAsync<T>(HttpMethod.Put, path, JsonContent.Create(body, options: Json));

    // For calls that return no body, such as DELETE or a password change.
    public async Task<ApiResult> SendWithoutResultAsync(HttpMethod method, string path, object? body = null)
    {
        var content = body is null ? null : JsonContent.Create(body, options: Json);
        return await SendAsync<object>(method, path, content);
    }

    // Sends an uploaded file to the API as multipart/form-data. The MVC app
    // does not store the file itself - the API puts it in Azure Blob Storage.
    public async Task<ApiResult<T>> PostFileAsync<T>(string path, IFormFile file, string fieldName = "File")
    {
        using var form = new MultipartFormDataContent();
        using var stream = file.OpenReadStream();
        using var fileContent = new StreamContent(stream);

        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);

        form.Add(fileContent, fieldName, file.FileName);

        return await SendAsync<T>(HttpMethod.Post, path, form);
    }

    // ------------------------------------------------------------------
    // The single place where requests are actually sent
    // ------------------------------------------------------------------

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, HttpContent? content)
    {
        using var request = new HttpRequestMessage(method, path.TrimStart('/')) { Content = content };

        // Attach the API session cookie for the logged-in user, if any.
        var apiCookie = Session?.GetApiCookie();
        if (!string.IsNullOrEmpty(apiCookie))
        {
            request.Headers.Add("Cookie", apiCookie);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach the RaceDay API at {BaseAddress}", _http.BaseAddress);
            return new ApiResult<T>
            {
                Success = false,
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Error = $"The RaceDay API could not be reached at {_http.BaseAddress}. Check that it is running."
            };
        }

        using (response)
        {
            StoreApiCookie(response);

            if (!response.IsSuccessStatusCode)
            {
                return new ApiResult<T>
                {
                    Success = false,
                    StatusCode = response.StatusCode,
                    Error = await ReadErrorAsync(response)
                };
            }

            T? data = default;
            if (response.StatusCode != HttpStatusCode.NoContent &&
                response.Content.Headers.ContentLength != 0)
            {
                var text = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    data = JsonSerializer.Deserialize<T>(text, Json);
                }
            }

            return new ApiResult<T> { Success = true, StatusCode = response.StatusCode, Data = data };
        }
    }

    // When the API sets or refreshes its session cookie, keep it.
    private void StoreApiCookie(HttpResponseMessage response)
    {
        if (Session is null || !response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            return;
        }

        foreach (var header in setCookies)
        {
            // "RaceDay.Session=CfDJ8...; path=/; samesite=lax; httponly"
            var nameAndValue = header.Split(';', 2)[0].Trim();
            if (nameAndValue.StartsWith(ApiSessionCookieName + "=", StringComparison.Ordinal))
            {
                Session.SetApiCookie(nameAndValue);
            }
        }
    }

    // Turns the API's error body into one readable sentence. The API returns
    // either { "message": "..." } or a validation problem with an "errors" list.
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();

        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("message", out var message) &&
                        message.ValueKind == JsonValueKind.String)
                    {
                        return message.GetString()!;
                    }

                    if (root.TryGetProperty("errors", out var errors) &&
                        errors.ValueKind == JsonValueKind.Object)
                    {
                        var messages = errors.EnumerateObject()
                            .Where(p => p.Value.ValueKind == JsonValueKind.Array)
                            .SelectMany(p => p.Value.EnumerateArray())
                            .Select(v => v.GetString())
                            .Where(s => !string.IsNullOrWhiteSpace(s));

                        var joined = string.Join(" ", messages);
                        if (!string.IsNullOrWhiteSpace(joined))
                        {
                            return joined;
                        }
                    }

                    if (root.TryGetProperty("title", out var title) &&
                        title.ValueKind == JsonValueKind.String)
                    {
                        return title.GetString()!;
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON - fall through to the generic message.
            }
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Please log in to continue.",
            HttpStatusCode.Forbidden => "You are not allowed to do that.",
            HttpStatusCode.NotFound => "That item could not be found.",
            _ => $"The API returned an error ({(int)response.StatusCode})."
        };
    }
}
