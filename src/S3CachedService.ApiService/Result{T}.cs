using S3CachedService.ApiService.Errors;

namespace S3CachedService.ApiService;

public class Result<T> : Result
{
    public T Value { get; }

    protected Result(T value)
        : base()
    {
        Value = value;
    }

    protected Result(ServiceError error)
        : base(error)
    {
        Value = default!;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(ServiceError error) => new(error);

    public static Result<T> Failure(string message) => new(new ServiceError(message));

    public static implicit operator Result<T>(T value) => Success(value);

    public void Deconstruct(out T valuie, out ServiceError error)
    {
        valuie = Value;
        error = Error;
    }
}
