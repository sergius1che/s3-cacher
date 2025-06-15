namespace S3CachedService.ApiService.Errors;

public class S3AccessDeniedError : S3ClientError
{
    public S3AccessDeniedError()
        : base("Access denied to the requested resource.")
    {
    }

    public override ErrorDetails GetDetails()
    {
        var details = base.GetDetails();

        details.HttpCode = StatusCodes.Status403Forbidden;
        details.Title = "Forbidden";

        return details;
    }
}
