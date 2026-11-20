
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using RaceDay.Api.Services;

public class BlobImageStorage : IImageStorage
{
    private readonly BlobContainerClient? _container;
    private readonly ILogger<BlobImageStorage> _logger;
    private bool _containerReady;

    public BlobImageStorage(IConfiguration configuration, ILogger<BlobImageStorage> logger)
    {
        _logger = logger;

        var connectionString = configuration["AzureStorage:ConnectionString"];
        var containerName = configuration["AzureStorage:ContainerName"] ?? "event-banners";

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            _container = new BlobServiceClient(connectionString).GetBlobContainerClient(containerName);
        }
    }

    public bool IsConfigured => _container is not null;

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType,
        CancellationToken cancellationToken = default)
    {
        var container = GetContainer();

        try
        {
            await EnsureContainerAsync(container, cancellationToken);

            var blob = container.GetBlobClient(fileName);
            await blob.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                    CacheControl = "public, max-age=86400"
                }
            }, cancellationToken);

            _logger.LogInformation("Uploaded {BlobName} to Azure Blob Storage", fileName);
            return blob.Uri.ToString();
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == "PublicAccessNotPermitted")
        {
            throw new ImageStorageException(
                "The storage account does not allow public blob access. In the Azure portal, open the " +
                "storage account, go to Settings > Configuration and enable 'Allow Blob anonymous access'.", ex);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Blob Storage upload failed");
            throw new ImageStorageException($"Azure Blob Storage rejected the upload ({ex.Status}).", ex);
        }
    }

    public async Task DeleteAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (_container is null || !Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        // Only delete blobs that live in this container.
        var parts = new BlobUriBuilder(uri);
        if (!string.Equals(parts.BlobContainerName, _container.Name, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(parts.BlobName))
        {
            return;
        }

        try
        {
            await _container.GetBlobClient(parts.BlobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex)
        {
            // An old banner that cannot be removed should not fail the request.
            _logger.LogWarning(ex, "Could not delete old blob {BlobName}", parts.BlobName);
        }
    }

    private BlobContainerClient GetContainer() =>
        _container ?? throw new ImageStorageException(
            "Azure Blob Storage is not configured. Set AzureStorage:ConnectionString in the API's user secrets.");

    // Creates the container the first time it is needed.
    private async Task EnsureContainerAsync(BlobContainerClient container, CancellationToken cancellationToken)
    {
        if (_containerReady)
        {
            return;
        }

        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
        _containerReady = true;
    }
}
