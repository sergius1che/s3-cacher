namespace S3CachedService.ApiService.Errors;

public class S3ObjectNotFoundError : S3ClientError
{
    public S3ObjectNotFoundError(string bucketName, string objectKey)
        : base($"Object '{objectKey}' not found in bucket '{bucketName}'.")
    {
        BucketName = bucketName;
        ObjectKey = objectKey;
    }

    public string BucketName { get; set; }

    public string ObjectKey { get; set; }

    public override ErrorDetails GetDetails()
    {
        var details = base.GetDetails();

        details.HttpCode = StatusCodes.Status404NotFound;
        details.Title = "Object not found";

        return details;
    }
}
