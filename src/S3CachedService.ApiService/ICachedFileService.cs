/// <summary>
/// Basic cache servie
/// </summary>
public interface ICachedFileService
{
    /// <summary>
    /// Get file and set it to HttpContext
    /// </summary>
    /// <param name="httpContext"></param>
    /// <returns></returns>
    Task GetFileAsync(HttpContext httpContext);
}
