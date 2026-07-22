using Microsoft.AspNetCore.StaticFiles;
using S3CachedService.ApiService;
using S3CachedService.ApiService.Cache;
using S3CachedService.ApiService.Errors;
using S3CachedService.ApiService.S3Client;

public class CachedFileService : ICachedFileService
{
    private readonly IS3Client _s3Client;
    private readonly IFileCache _cache;

    public CachedFileService(IS3Client s3Client, IFileCache cache)
    {
        _s3Client = s3Client;
        _cache = cache;
    }

    public async Task GetFileAsync(HttpContext httpContext)
    {
        var path = httpContext.Request.RouteValues["path"] as string;

        if (string.IsNullOrEmpty(path))
        {
            var valodateError = new ValidateError("Path is required");

            await valodateError.WriteToAsync(httpContext);

            return;
        }

        var reqParameters = RequestParameters.From(httpContext.Request);
        var (bucket, objectKey) = GetParts(path);
        var userFileName = reqParameters.FileName ?? objectKey;
        var mimeType = GetMimeType(userFileName);

        var contentDisposition = new System.Net.Mime.ContentDisposition
        {
            Inline = ShouldDisplayInBrowser(mimeType), // true - показывать в браузере, false - скачивать
            FileName = userFileName
        };

        if (string.IsNullOrEmpty(bucket))
        {
            var valodateError = new ValidateError("Bucket name is required");

            await valodateError.WriteToAsync(httpContext);

            return;
        }

        var (cachedFile, cacheError) = await _cache.GetFileAsync(bucket, objectKey, reqParameters);

        if (cacheError is null)
        {
            httpContext.Response.Headers.ContentDisposition = contentDisposition.ToString();
            httpContext.Response.ContentType = mimeType;

            using (cachedFile)
            {
                await cachedFile.CopyToPipeAsync(httpContext.Response.BodyWriter, ct: httpContext.RequestAborted);
            }

            return;
        }

        if (cacheError is not CacheNotFoundError)
        {
            await cacheError.WriteToAsync(httpContext);

            return;
        }

        var (s3Stream, err) = await _s3Client.GetObjectStreamAsync(bucket, objectKey, httpContext.RequestAborted);

        if (err != null)
        {
            await err.WriteToAsync(httpContext);
            return;
        }

        httpContext.Response.Headers.ContentDisposition = contentDisposition.ToString();
        httpContext.Response.ContentType = mimeType;

        using (s3Stream)
        {
            var result = await _cache.SaveStreamAsync(bucket, objectKey, s3Stream, httpContext.Response.BodyWriter, httpContext.RequestAborted);

            if (result.Error != null)
            {
                await result.Error.WriteToAsync(httpContext);
                return;
            }
        }
    }

    private (string? path, string fileName) GetParts(string path)
    {
        var spn = path.AsSpan();
        var idx = spn.IndexOf("/", StringComparison.Ordinal);

        if (idx == -1)
        {
            return (null, path);
        }

        return (spn[..idx].ToString(), spn[(idx + 1)..].ToString());
    }

    private string GetMimeType(string fileName)
    {
        var provider = new FileExtensionContentTypeProvider();
        if (provider.TryGetContentType(fileName, out var contentType))
        {
            return contentType;
        }

        return "application/octet-stream";
    }

    private bool ShouldDisplayInBrowser(string mimeType)
    {
        var displayableMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp",
            "image/svg+xml",
            "application/pdf",
            "text/plain",
            "text/html",
            "application/xhtml+xml"
        };

        return displayableMimeTypes.Contains(mimeType);
    }
}
