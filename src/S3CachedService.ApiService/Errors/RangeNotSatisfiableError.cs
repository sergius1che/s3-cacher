namespace S3CachedService.ApiService.Errors;

/// <summary>
/// Classic HTTP error 416 for range requests
/// </summary>
public class RangeNotSatisfiableError : ServiceError
{
    public RangeNotSatisfiableError(long totalLength)
        : base($"Requested range is not satisfiable for an object of {totalLength} bytes.")
    {
    }

    /// <inheritdoc/>
    public override ErrorDetails GetDetails()
    {
        var details = base.GetDetails();

        details.Title = "Range not satisfiable";
        details.HttpCode = StatusCodes.Status416RangeNotSatisfiable;

        return details;
    }
}
