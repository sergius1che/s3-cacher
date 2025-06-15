using System.Net;

namespace S3CachedService.ApiService.Errors;

public class ValidateError : ServiceError
{
    public ValidateError(string message) 
        : base(message)
    {
    }

    public override ErrorDetails GetDetails()
    {
        var details = base.GetDetails();

        details.Title = "Bad request!";
        details.HttpCode = (int)HttpStatusCode.BadRequest;

        return details;
    }
}
