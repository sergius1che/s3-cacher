using S3CachedService.ApiService.Errors;

namespace S3CachedService.ApiService;

public class Result
{
    public bool IsSuccess { get; }

    public ServiceError Error { get; }

    public Result()
    {
        IsSuccess = true;
        Error = null!;
    }

    public Result(ServiceError error)
    {
        IsSuccess = false;
        Error = error;
    }

    public static implicit operator Result(ServiceError error)
    {
        return new Result(error);
    }
}
