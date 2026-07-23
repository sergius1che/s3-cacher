using System.IO.Pipelines;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using S3CachedService.ApiService.Cache.SimpleQueue;
using S3CachedService.ApiService.S3Client;

namespace S3CachedService.Tests;

public class SimpleFileCacheTests : IDisposable
{
    private readonly string _dataPath = Path.Combine(
        Path.GetTempPath(), "s3-cacher-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
        {
            Directory.Delete(_dataPath, recursive: true);
        }
    }

    [Fact]
    public async Task SaveStreamAsync_TwoFiles_TotalBytesEqualsPayloadSum()
    {
        var cache = CreateCache(new SimpleQueueSettings { DataPath = _dataPath });

        await SaveAsync(cache, "bucket", "a.bin", payloadSize: 100);
        await SaveAsync(cache, "bucket", "b.bin", payloadSize: 250);

        Assert.Equal(350, cache.TotalBytes);
    }

    [Fact]
    public async Task StartAsync_RestoresTotalBytesFromDisk()
    {
        var settings = new SimpleQueueSettings { DataPath = _dataPath };
        var firstCache = CreateCache(settings);
        await SaveAsync(firstCache, "bucket", "a.bin", payloadSize: 100);
        await SaveAsync(firstCache, "bucket", "b.bin", payloadSize: 250);

        var restoredCache = CreateCache(settings);
        await restoredCache.StartAsync(CancellationToken.None);
        await restoredCache.StopAsync(CancellationToken.None);

        Assert.Equal(350, restoredCache.TotalBytes);
    }

    [Fact]
    public async Task CleanupInternal_EvictionByMaxCount_SubtractsEvictedSize()
    {
        var cache = CreateCache(new SimpleQueueSettings { DataPath = _dataPath, MaxCount = 1 });

        await SaveAsync(cache, "bucket", "a.bin", payloadSize: 100);
        await SaveAsync(cache, "bucket", "b.bin", payloadSize: 250);

        cache.CleanupInternal();

        Assert.Equal(250, cache.TotalBytes);
    }

    [Fact]
    public async Task CleanupInternal_EvictionByMaxBytes_EvictsOldestUntilUnderLimit()
    {
        var cache = CreateCache(new SimpleQueueSettings
        {
            DataPath = _dataPath,
            MaxCount = 100,
            MaxBytes = 300,
        });

        await SaveAsync(cache, "bucket", "a.bin", payloadSize: 100);
        await SaveAsync(cache, "bucket", "b.bin", payloadSize: 250);

        cache.CleanupInternal();

        Assert.Equal(250, cache.TotalBytes);
        Assert.False(File.Exists(Path.Combine(_dataPath, "bucket", "a.bin")));
        Assert.True(File.Exists(Path.Combine(_dataPath, "bucket", "b.bin")));
    }

    private static SimpleFileCache CreateCache(SimpleQueueSettings settings)
    {
        return new SimpleFileCache(Options.Create(settings), NullLogger<SimpleFileCache>.Instance);
    }

    private static async Task SaveAsync(SimpleFileCache cache, string bucket, string objectKey, int payloadSize)
    {
        using var source = new MemoryStream(new byte[payloadSize]);
        var s3Stream = new S3ObjectStream(source, payloadSize);
        var response = PipeWriter.Create(new MemoryStream());

        var result = await cache.SaveStreamAsync(bucket, objectKey, s3Stream, response);

        Assert.True(result.IsSuccess);
    }
}
