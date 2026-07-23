namespace S3CachedService.ApiService;

public class ErrorDetails
{
    public int HttpCode { get; set; }

    public string? Title { get; set; }

    public string? Details { get; set; }
}
