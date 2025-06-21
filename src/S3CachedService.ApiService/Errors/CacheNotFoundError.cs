using System.Net;

namespace S3CachedService.ApiService.Errors;

/// <summary>
/// File not found in cache storage
/// </summary>
public class CacheNotFoundError : ServiceError
{
    public CacheNotFoundError(string bucketName, string objectKey)
        : base($"Cached file '{bucketName}/{objectKey}' not found")
    {
    }

    /// <inheritdoc/>
    public override ErrorDetails GetDetails()
    {
        var details = base.GetDetails();

        details.Title = "Not found";
        details.HttpCode = (int)HttpStatusCode.NotFound;

        return details;
    }
}
