using System.Net;

namespace S3CachedService.ApiService.Errors;

public class CacheNotFoundError : ServiceError
{
    public CacheNotFoundError(string bucketName, string objectKey)
        : base($"Cached file '{bucketName}/{objectKey}' not found")
    {
    }

    public override ErrorDetails GetDetails()
    {
        var details = base.GetDetails();

        details.Title = "Not found";
        details.HttpCode = (int)HttpStatusCode.NotFound;

        return details;
    }
}
