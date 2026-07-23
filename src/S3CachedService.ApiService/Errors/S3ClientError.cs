namespace S3CachedService.ApiService.Errors;

/// <summary>
/// Error on S3 storage request
/// </summary>
public class S3ClientError : ServiceError
{
    public S3ClientError(string message)
        : base(message)
    {
    }
}