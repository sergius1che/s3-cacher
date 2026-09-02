using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using S3CachedService.ApiService;
using S3CachedService.ApiService.Cache.SimpleQueue;
using S3CachedService.ApiService.S3Client;

namespace S3CachedService.Tests;

public class CachedFileServiceContentDispositionTests : IDisposable
{
    // Кириллическое имя длиннее ~23 символов — порог, после которого почтовый
    // MIME-энкодер сворачивает base64 encoded-word через CRLF (line folding).
    private const string LongCyrillicName = "Ежемесячный отчет по продажам за январь 2026.pdf";

    private static readonly byte[] _payload = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"u8.ToArray();

    private readonly string _dataPath = Path.Combine(
        Path.GetTempPath(), "s3-cacher-tests", Guid.NewGuid().ToString("N"));

    private readonly SimpleFileCache _cache;
    private readonly FakeS3Client _s3Client = new(_payload);
    private readonly CachedFileService _service;

    public CachedFileServiceContentDispositionTests()
    {
        _cache = new SimpleFileCache(
            Options.Create(new SimpleQueueSettings { DataPath = _dataPath }),
            NullLogger<SimpleFileCache>.Instance);
        _service = new CachedFileService(_s3Client, _cache);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
        {
            Directory.Delete(_dataPath, recursive: true);
        }
    }

    [Fact]
    public async Task GetFileAsync_CacheMissWithLongNonAsciiObjectKey_ContentDispositionHasNoControlCharacters()
    {
        var context = CreateContext($"bucket/{LongCyrillicName}");

        await _service.GetFileAsync(context);

        AssertHeaderIsValidForHttp(context);
    }

    [Fact]
    public async Task GetFileAsync_CacheHitWithLongNonAsciiObjectKey_ContentDispositionHasNoControlCharacters()
    {
        await SeedCacheAsync("bucket", LongCyrillicName);

        var context = CreateContext($"bucket/{LongCyrillicName}");

        await _service.GetFileAsync(context);

        AssertHeaderIsValidForHttp(context);
    }

    [Fact]
    public async Task GetFileAsync_LongNonAsciiFileNameQueryParameter_ContentDispositionHasNoControlCharacters()
    {
        var context = CreateContext("bucket/file.bin", $"?fileName={Uri.EscapeDataString(LongCyrillicName)}");

        await _service.GetFileAsync(context);

        AssertHeaderIsValidForHttp(context);
    }

    [Fact]
    public async Task GetFileAsync_NonAsciiFileName_UsesRfc6266FileNameStar()
    {
        var context = CreateContext("bucket/file.bin", $"?fileName={Uri.EscapeDataString(LongCyrillicName)}");

        await _service.GetFileAsync(context);

        var header = context.Response.Headers.ContentDisposition.ToString();

        // HTTP не декодирует MIME encoded-word (RFC 2047) — юникодное имя
        // передаётся через filename* (RFC 6266 / RFC 5987).
        Assert.DoesNotContain("=?utf-8?", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("filename*=UTF-8''", header, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("Ежемесячный"), header, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetFileAsync_FileNameWithCrLf_ContentDispositionHasNoControlCharacters()
    {
        var context = CreateContext("bucket/file.bin", $"?fileName={Uri.EscapeDataString("inject\r\nX-Evil: 1.bin")}");

        await _service.GetFileAsync(context);

        AssertHeaderIsValidForHttp(context);
    }

    [Fact]
    public async Task GetFileAsync_AsciiFileName_KeepsInlineDispositionForDisplayableMimeType()
    {
        var context = CreateContext("bucket/picture.png");

        await _service.GetFileAsync(context);

        var header = context.Response.Headers.ContentDisposition.ToString();

        Assert.StartsWith("inline", header, StringComparison.Ordinal);
        Assert.Contains("picture.png", header, StringComparison.Ordinal);
        Assert.Equal("image/png", context.Response.ContentType);
    }

    /// <summary>
    /// Kestrel валидирует заголовки ответа и падает
    /// с InvalidOperationException на любом control-символе (0x000D и т.п.).
    /// DefaultHttpContext такой проверки не делает, поэтому проверяем значение сами.
    /// </summary>
    private static void AssertHeaderIsValidForHttp(HttpContext context)
    {
        var header = context.Response.Headers.ContentDisposition.ToString();

        Assert.NotEmpty(header);
        Assert.DoesNotContain('\r', header);
        Assert.DoesNotContain('\n', header);
        Assert.All(header, c => Assert.InRange(c, ' ', (char)0x7E));
    }

    private async Task SeedCacheAsync(string bucket, string objectKey)
    {
        using var source = new MemoryStream(_payload);
        var s3Stream = new S3ObjectStream(source, _payload.Length);
        var discard = System.IO.Pipelines.PipeWriter.Create(Stream.Null);

        var result = await _cache.SaveStreamAsync(bucket, objectKey, s3Stream, discard);

        Assert.True(result.IsSuccess);
    }

    private static DefaultHttpContext CreateContext(string path, string? queryString = null)
    {
        var context = new DefaultHttpContext();

        context.Request.RouteValues["path"] = path;
        context.Response.Body = new MemoryStream();

        if (queryString != null)
        {
            context.Request.QueryString = new QueryString(queryString);
        }

        return context;
    }

    private class FakeS3Client : IS3Client
    {
        private readonly byte[] _payload;

        public FakeS3Client(byte[] payload)
        {
            _payload = payload;
        }

        public Task<Result<S3ObjectStream>> GetObjectStreamAsync(string bucketName, string objectKey, CancellationToken ct = default)
        {
            return Task.FromResult(Result<S3ObjectStream>.Success(
                new S3ObjectStream(new MemoryStream(_payload), _payload.Length)));
        }

        public Task<Result<byte[]>> GetObjectBytesAsync(string bucketName, string objectKey, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<string>> GetObjectAsStringAsync(string bucketName, string objectKey, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<bool>> DoesObjectExistAsync(string bucketName, string objectKey, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result> UploadObjectFromStreamAsync(string bucketName, string objectKey, Stream data, string contentType, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result> DeleteObjectAsync(string bucketName, string objectKey, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
