namespace S3CachedService.ApiService.Errors;

/// <summary>
/// S3 clietn internal error
/// </summary>
public class S3InternalServerError : S3ClientError
{
    public S3InternalServerError(string message)
        : base($"Internal server error occurred: {message}")
    {
    }
}
