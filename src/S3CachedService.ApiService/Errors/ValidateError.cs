using System.Net;

namespace S3CachedService.ApiService.Errors;

/// <summary>
/// Client validation data error
/// </summary>
public class ValidateError : ServiceError
{
    /// <summary>
    /// Create instance of <see cref="ValidateError"/> with validation message
    /// </summary>
    /// <param name="message"></param>
    public ValidateError(string message) 
        : base(message)
    {
    }

    /// <inheritdoc/>
    public override ErrorDetails GetDetails()
    {
        var details = base.GetDetails();

        details.Title = "Bad request!";
        details.HttpCode = (int)HttpStatusCode.BadRequest;

        return details;
    }
}
