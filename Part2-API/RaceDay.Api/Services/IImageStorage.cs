namespace RaceDay.Api.Services;

// Stores event banner images. The real implementation uses Azure Blob
// Storage; the unit tests use an in-memory fake.
public interface IImageStorage
{
    // False when no storage connection string has been configured.
    bool IsConfigured { get; }

    // Saves the image and returns its public URL.
    Task<string> UploadAsync(Stream content, string fileName, string contentType,
        CancellationToken cancellationToken = default);

    // Removes an image previously returned by UploadAsync. Unknown URLs are ignored.
    Task DeleteAsync(string imageUrl, CancellationToken cancellationToken = default);
}

// Raised when the storage service cannot complete a request, with a message
// that is safe to show to the user.
public class ImageStorageException : Exception
{
    public ImageStorageException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
