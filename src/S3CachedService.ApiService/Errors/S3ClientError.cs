namespace S3CachedService.ApiService.Errors;

public class S3ClientError : ServiceError
{
    public S3ClientError(string message)
        : base(message)
    {
    }
}