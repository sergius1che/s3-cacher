using System.Text.Json;
using System.Text.Json.Serialization;

namespace S3CachedService.ApiService.Errors;

/// <summary>
/// Basic service error
/// </summary>
public class ServiceError
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ServiceError(string description)
    {
        Description = description;
    }

    /// <summary>
    /// Error description
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Write error as json to <see cref="HttpContext" />
    /// </summary>
    /// <param name="context"><see cref="HttpContext"/> of the current HTTP request</param>
    /// <returns>Async task</returns>
    public Task WriteToAsync(HttpContext context)
    {
        var details = GetDetails();

        context.Response.StatusCode = details.HttpCode;
        context.Response.ContentType = "application/json";

        var jsonData = JsonSerializer.Serialize(details, _jsonOptions);

        return context.Response.WriteAsync(jsonData, context.RequestAborted);
    }

    /// <summary>
    /// Details by current error
    /// </summary>
    /// <returns><see cref="ErrorDetails"/> of the current error</returns>
    public virtual ErrorDetails GetDetails() 
    {
        return new ErrorDetails
        {
            HttpCode = StatusCodes.Status500InternalServerError,
            Title = "Internal service error",
            Details = Description
        };
    }
}
