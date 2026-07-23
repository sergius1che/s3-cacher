namespace S3CachedService.ApiService.Errors;

/// <summary>
/// Classic HTTP error 404 for S3 client
/// </summary>
public class S3ObjectNotFoundError : S3ClientError
{
    public S3ObjectNotFoundError(string bucketName, string objectKey)
        : base($"Object '{objectKey}' not found in bucket '{bucketName}'.")
    {
        BucketName = bucketName;
        ObjectKey = objectKey;
    }

    /// <summary>
    /// S3 storage bucket
    /// </summary>
    public string BucketName { get; set; }

    /// <summary>
    /// Object storage unique indentifier
    /// </summary>
    public string ObjectKey { get; set; }

    /// <inheritdoc/>
    public override ErrorDetails GetDetails()
    {
        var details = base.GetDetails();

        details.HttpCode = StatusCodes.Status404NotFound;
        details.Title = "Object not found";

        return details;
    }
}
