namespace S3CachedService.ApiService.Errors;

public class S3InternalServerError : S3ClientError
{
    public S3InternalServerError(string message)
        : base($"Internal server error occurred: {message}")
    {
    }
}
