using System.Text.Json;
using System.Text.Json.Serialization;

namespace S3CachedService.ApiService.Errors;

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

    public string Description { get; }

    public Task WriteToAsync(HttpContext context)
    {
        var details = GetDetails();

        context.Response.StatusCode = details.HttpCode;
        context.Response.ContentType = "application/json";

        var jsonData = JsonSerializer.Serialize(details, _jsonOptions);

        return context.Response.WriteAsync(jsonData, context.RequestAborted);
    }

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
