using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;
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

        var range = GetSingleRange(httpContext.Request);

        if (cacheError is null)
        {
            SetContentHeaders(httpContext, contentDisposition, mimeType);

            using (cachedFile)
            {
                await WriteFileAsync(httpContext, cachedFile, range);
            }

            return;
        }

        if (cacheError is not CacheNotFoundError)
        {
            await cacheError.WriteToAsync(httpContext);

            return;
        }

        var (s3Object, err) = await _s3Client.GetObjectStreamAsync(bucket, objectKey, httpContext.RequestAborted);

        if (err != null)
        {
            await err.WriteToAsync(httpContext);
            return;
        }

        using (s3Object)
        {
            ByteRange? window = null;

            if (range is not null)
            {
                if (!TryResolveRange(range, s3Object.Length, out var resolved))
                {
                    await WriteRangeNotSatisfiableAsync(httpContext, s3Object.Length);

                    return;
                }

                window = resolved;
            }

            SetContentHeaders(httpContext, contentDisposition, mimeType);

            Result result;

            if (window is null)
            {
                result = await _cache.SaveStreamAsync(bucket, objectKey, s3Object, httpContext.Response.BodyWriter, httpContext.RequestAborted);
            }
            else
            {
                // Range при промахе: файл целиком закачивается в кэш,
                // в ответ клиенту пишутся только байты запрошенного окна.
                SetPartialContentHeaders(httpContext, window.Value, s3Object.Length);

                result = await _cache.SaveStreamAsync(bucket, objectKey, s3Object, httpContext.Response.BodyWriter, window.Value, httpContext.RequestAborted);
            }

            if (result.Error != null)
            {
                await result.Error.WriteToAsync(httpContext);
                return;
            }
        }
    }

    private static void SetContentHeaders(HttpContext httpContext, System.Net.Mime.ContentDisposition contentDisposition, string mimeType)
    {
        httpContext.Response.Headers.ContentDisposition = contentDisposition.ToString();
        httpContext.Response.Headers.AcceptRanges = "bytes";
        httpContext.Response.ContentType = mimeType;
    }

    private static RangeItemHeaderValue? GetSingleRange(HttpRequest request)
    {
        var range = request.GetTypedHeaders().Range;

        if (range is null
            || !range.Unit.Equals("bytes", StringComparison.OrdinalIgnoreCase)
            || range.Ranges.Count != 1)
        {
            return null;
        }

        return range.Ranges.Single();
    }

    private static async Task WriteFileAsync(HttpContext httpContext, Stream file, RangeItemHeaderValue? range)
    {
        if (range is null)
        {
            await file.CopyToPipeAsync(httpContext.Response.BodyWriter, ct: httpContext.RequestAborted);

            return;
        }

        var total = file.Length - file.Position;

        if (!TryResolveRange(range, total, out var window))
        {
            // Заголовки контента уже выставлены под успешный ответ — убираем лишний.
            httpContext.Response.Headers.Remove(HeaderNames.ContentDisposition);

            await WriteRangeNotSatisfiableAsync(httpContext, total);

            return;
        }

        SetPartialContentHeaders(httpContext, window, total);

        file.Seek(window.From, SeekOrigin.Current);

        await file.CopyToPipeAsync(httpContext.Response.BodyWriter, window.Length, ct: httpContext.RequestAborted);
    }

    /// <summary>
    /// Разрешает запрошенный диапазон в абсолютное окно [From..To] по полной длине объекта.
    /// false — диапазон не satisfiable (416).
    /// </summary>
    private static bool TryResolveRange(RangeItemHeaderValue range, long total, out ByteRange window)
    {
        long from, to;

        if (range.From is null)
        {
            // Суффиксный диапазон bytes=-N: последние N байт файла.
            from = Math.Max(0, total - range.To!.Value);
            to = total - 1;
        }
        else
        {
            from = range.From.Value;
            to = Math.Min(range.To ?? long.MaxValue, total - 1);
        }

        window = new ByteRange(from, to);

        return from < total && from <= to;
    }

    private static void SetPartialContentHeaders(HttpContext httpContext, ByteRange window, long total)
    {
        httpContext.Response.StatusCode = StatusCodes.Status206PartialContent;
        httpContext.Response.Headers.ContentRange = new ContentRangeHeaderValue(window.From, window.To, total).ToString();
    }

    private static Task WriteRangeNotSatisfiableAsync(HttpContext httpContext, long total)
    {
        httpContext.Response.Headers.ContentRange = new ContentRangeHeaderValue(total).ToString();

        return new RangeNotSatisfiableError(total).WriteToAsync(httpContext);
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
