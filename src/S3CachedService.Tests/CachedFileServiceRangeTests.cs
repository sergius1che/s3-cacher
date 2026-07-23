using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using S3CachedService.ApiService;
using S3CachedService.ApiService.Cache.SimpleQueue;
using S3CachedService.ApiService.S3Client;

namespace S3CachedService.Tests;

public class CachedFileServiceRangeTests : IDisposable
{
    private static readonly byte[] _payload = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"u8.ToArray();

    private readonly string _dataPath = Path.Combine(
        Path.GetTempPath(), "s3-cacher-tests", Guid.NewGuid().ToString("N"));

    private readonly SimpleFileCache _cache;
    private readonly FakeS3Client _s3Client = new(_payload);
    private readonly CachedFileService _service;

    public CachedFileServiceRangeTests()
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
    public async Task GetFileAsync_WithoutRange_Returns200FullBodyWithAcceptRanges()
    {
        await SeedCacheAsync("bucket", "file.bin");

        var context = CreateContext("bucket/file.bin");

        await _service.GetFileAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("bytes", context.Response.Headers.AcceptRanges.ToString());
        Assert.Equal(_payload, GetBody(context));
    }

    [Fact]
    public async Task GetFileAsync_CacheHitWithBoundedRange_Returns206WithContentRange()
    {
        await SeedCacheAsync("bucket", "file.bin");

        var context = CreateContext("bucket/file.bin", rangeHeader: "bytes=2-5");

        await _service.GetFileAsync(context);

        Assert.Equal(StatusCodes.Status206PartialContent, context.Response.StatusCode);
        Assert.Equal($"bytes 2-5/{_payload.Length}", context.Response.Headers.ContentRange.ToString());
        Assert.Equal(_payload[2..6], GetBody(context));
    }

    [Theory]
    [InlineData("bytes=20-", 20, 25)]   // открытый — до конца файла
    [InlineData("bytes=-4", 22, 25)]    // суффиксный — последние 4 байта
    [InlineData("bytes=2-100", 2, 25)]  // to за концом файла — обрезается
    [InlineData("bytes=-100", 0, 25)]   // суффикс длиннее файла — весь файл
    public async Task GetFileAsync_CacheHitWithOpenOrSuffixRange_Returns206(string rangeHeader, int expectedFrom, int expectedTo)
    {
        await SeedCacheAsync("bucket", "file.bin");

        var context = CreateContext("bucket/file.bin", rangeHeader);

        await _service.GetFileAsync(context);

        Assert.Equal(StatusCodes.Status206PartialContent, context.Response.StatusCode);
        Assert.Equal($"bytes {expectedFrom}-{expectedTo}/{_payload.Length}", context.Response.Headers.ContentRange.ToString());
        Assert.Equal(_payload[expectedFrom..(expectedTo + 1)], GetBody(context));
    }

    [Theory]
    [InlineData("bytes=26-")]     // from == total
    [InlineData("bytes=100-200")] // from за концом файла
    [InlineData("bytes=-0")]      // пустой суффикс
    public async Task GetFileAsync_UnsatisfiableRange_Returns416WithContentRange(string rangeHeader)
    {
        await SeedCacheAsync("bucket", "file.bin");

        var context = CreateContext("bucket/file.bin", rangeHeader);

        await _service.GetFileAsync(context);

        Assert.Equal(StatusCodes.Status416RangeNotSatisfiable, context.Response.StatusCode);
        Assert.Equal($"bytes */{_payload.Length}", context.Response.Headers.ContentRange.ToString());
    }

    [Fact]
    public async Task GetFileAsync_CacheMissWithRange_CachesFullFileAndReturnsSlice()
    {
        var context = CreateContext("bucket/file.bin", rangeHeader: "bytes=2-5");

        await _service.GetFileAsync(context);

        Assert.Equal(StatusCodes.Status206PartialContent, context.Response.StatusCode);
        Assert.Equal($"bytes 2-5/{_payload.Length}", context.Response.Headers.ContentRange.ToString());
        Assert.Equal(_payload[2..6], GetBody(context));
        Assert.Equal(1, _s3Client.GetObjectStreamCalls);

        // Повторный range-запрос обслуживается из кэша, без похода в S3.
        var secondContext = CreateContext("bucket/file.bin", rangeHeader: "bytes=6-7");

        await _service.GetFileAsync(secondContext);

        Assert.Equal(StatusCodes.Status206PartialContent, secondContext.Response.StatusCode);
        Assert.Equal(_payload[6..8], GetBody(secondContext));
        Assert.Equal(1, _s3Client.GetObjectStreamCalls);
    }

    [Theory]
    [InlineData("bytes=0-1,3-4")]  // несколько диапазонов — не поддерживаем
    [InlineData("bytes=5-2")]      // from > to — синтаксически невалиден
    [InlineData("items=0-5")]      // unit не bytes
    public async Task GetFileAsync_IgnoredRangeHeader_Returns200FullBody(string rangeHeader)
    {
        await SeedCacheAsync("bucket", "file.bin");

        var context = CreateContext("bucket/file.bin", rangeHeader);

        await _service.GetFileAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(_payload, GetBody(context));
    }

    [Fact]
    public async Task GetFileAsync_CacheMissWithUnsatisfiableRange_Returns416WithoutServingBody()
    {
        var context = CreateContext("bucket/file.bin", rangeHeader: "bytes=100-200");

        await _service.GetFileAsync(context);

        Assert.Equal(StatusCodes.Status416RangeNotSatisfiable, context.Response.StatusCode);
        Assert.Equal($"bytes */{_payload.Length}", context.Response.Headers.ContentRange.ToString());
        Assert.Equal(1, _s3Client.GetObjectStreamCalls);
    }

    private async Task SeedCacheAsync(string bucket, string objectKey)
    {
        using var source = new MemoryStream(_payload);
        var s3Stream = new S3ObjectStream(source, _payload.Length);
        var discard = System.IO.Pipelines.PipeWriter.Create(Stream.Null);

        var result = await _cache.SaveStreamAsync(bucket, objectKey, s3Stream, discard);

        Assert.True(result.IsSuccess);
    }

    private static DefaultHttpContext CreateContext(string path, string? rangeHeader = null)
    {
        var context = new DefaultHttpContext();

        context.Request.RouteValues["path"] = path;
        context.Response.Body = new MemoryStream();

        if (rangeHeader != null)
        {
            context.Request.Headers.Range = rangeHeader;
        }

        return context;
    }

    private static byte[] GetBody(HttpContext context)
    {
        return ((MemoryStream)context.Response.Body).ToArray();
    }

    private class FakeS3Client : IS3Client
    {
        private readonly byte[] _payload;

        public FakeS3Client(byte[] payload)
        {
            _payload = payload;
        }

        public int GetObjectStreamCalls { get; private set; }

        public Task<Result<S3ObjectStream>> GetObjectStreamAsync(string bucketName, string objectKey, CancellationToken ct = default)
        {
            GetObjectStreamCalls++;

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
