using System.Net;

namespace S3CachedService.ApiService.Errors;

/// <summary>
/// File alredy added in cache
/// </summary>
public class CacheAlredyAddedError : ServiceError
{
    public CacheAlredyAddedError(string bucketName, string objectKey)
        : base($"File already exists '{bucketName}/{objectKey}'")
    {
    }

    /// <inheritdoc/>
    public override ErrorDetails GetDetails()
    {
        var details = base.GetDetails();

        details.Title = "Already exists";
        details.HttpCode = (int)HttpStatusCode.Conflict;

        return details;
    }
}
