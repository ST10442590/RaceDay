using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Controllers;
using RaceDay.Api.Models;
using RaceDay.Api.Models.Dtos;
using RaceDay.Api.Services;
using Xunit;

namespace RaceDay.Api.Tests;

// Covers uploading event banners. Azure Blob Storage is replaced with an
// in-memory fake, so the tests never touch the cloud.
public class EventImagesControllerTests
{
    private static EventImageUploadRequest Upload(string contentType = "image/jpeg", int size = 1024)
    {
        var bytes = Encoding.ASCII.GetBytes(new string('x', size));
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", "banner.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

        return new EventImageUploadRequest { File = file };
    }

    [Fact]
    public async Task UploadImage_AsOwner_StoresTheBlobUrlOnTheEvent()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var storage = new FakeImageStorage();
        var raceEvent = TestDatabase.TestEvent(db);

        var controller = new EventImagesController(db, storage);
        TestDatabase.SignIn(controller, TestDatabase.Organiser(db), RoleNames.Organiser);

        var result = await controller.UploadImage(raceEvent.EventId, Upload());

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var detail = Assert.IsType<EventDetailResponse>(ok.Value);

        Assert.Single(storage.Blobs);
        Assert.Equal(storage.Blobs.Keys.Single(), detail.ImageUrl);
        Assert.Equal(detail.ImageUrl, db.Events.Single(e => e.EventId == raceEvent.EventId).ImageUrl);
    }

    [Fact]
    public async Task UploadImage_ToAnotherOrganisersEvent_IsForbidden()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var storage = new FakeImageStorage();

        var controller = new EventImagesController(db, storage);
        TestDatabase.SignIn(controller, TestDatabase.OtherOrganiser(db), RoleNames.Organiser);

        var result = await controller.UploadImage(TestDatabase.TestEvent(db).EventId, Upload());

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Empty(storage.Blobs);
    }

    [Fact]
    public async Task UploadImage_ThatIsNotAnImage_IsRejected()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var storage = new FakeImageStorage();

        var controller = new EventImagesController(db, storage);
        TestDatabase.SignIn(controller, TestDatabase.Organiser(db), RoleNames.Organiser);

        var result = await controller.UploadImage(TestDatabase.TestEvent(db).EventId, Upload("application/pdf"));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(storage.Blobs);
    }

    [Fact]
    public async Task UploadImage_ReplacingABanner_DeletesTheOldBlob()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var storage = new FakeImageStorage();
        var eventId = TestDatabase.TestEvent(db).EventId;

        var controller = new EventImagesController(db, storage);
        TestDatabase.SignIn(controller, TestDatabase.Organiser(db), RoleNames.Organiser);

        await controller.UploadImage(eventId, Upload());
        var firstUrl = storage.Blobs.Keys.Single();

        await controller.UploadImage(eventId, Upload("image/png"));

        Assert.Single(storage.Blobs);
        Assert.DoesNotContain(firstUrl, storage.Blobs.Keys);
        Assert.EndsWith(".png", db.Events.Single(e => e.EventId == eventId).ImageUrl);
    }

    [Fact]
    public async Task UploadImage_WhenStorageIsNotConfigured_Returns503()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var storage = new FakeImageStorage { IsConfigured = false };

        var controller = new EventImagesController(db, storage);
        TestDatabase.SignIn(controller, TestDatabase.Organiser(db), RoleNames.Organiser);

        var result = await controller.UploadImage(TestDatabase.TestEvent(db).EventId, Upload());

        var unavailable = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
    }
}

// Stands in for Azure Blob Storage: keeps "uploaded" images in a dictionary
// keyed by a fake blob URL.
public class FakeImageStorage : IImageStorage
{
    public Dictionary<string, string> Blobs { get; } = new();

    public bool IsConfigured { get; set; } = true;

    public Task<string> UploadAsync(Stream content, string fileName, string contentType,
        CancellationToken cancellationToken = default)
    {
        var url = $"https://fake.blob.core.windows.net/event-banners/{fileName}";
        Blobs[url] = contentType;
        return Task.FromResult(url);
    }

    public Task DeleteAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        Blobs.Remove(imageUrl);
        return Task.CompletedTask;
    }
}
