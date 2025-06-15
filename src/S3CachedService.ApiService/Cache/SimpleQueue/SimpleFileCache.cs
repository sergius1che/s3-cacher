using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using S3CachedService.ApiService.Errors;

namespace S3CachedService.ApiService.Cache.SimpleQueue;

public class SimpleFileCache : IFileCache, IHostedService
{
    private readonly SimpleQueueSettings _simpleQueueSettings;

    private readonly ConcurrentDictionary<string, SimpleInfo> _cachedFiles;
    private readonly ConcurrentQueue<SimpleInfo> _cacheQueue;
    private readonly ILogger<SimpleFileCache> _logger;

    private Task? _cleanupTask;
    private bool _stopped;

    public SimpleFileCache(
        IOptions<SimpleQueueSettings> options,
        ILogger<SimpleFileCache> logger)
    {
        _simpleQueueSettings = options.Value;

        if (!Directory.Exists(_simpleQueueSettings.DataPath))
        {
            Directory.CreateDirectory(_simpleQueueSettings.DataPath);
        }

        _cachedFiles = new(-1, _simpleQueueSettings.MaxCount);
        _cacheQueue = new();
        _stopped = false;
        _logger = logger;
    }

    public async Task<Result<Stream>> GetFileAsync(string bucketName, string objectKey, RequestParameters parameters, CancellationToken ct = default)
    {
        if (!_cachedFiles.TryGetValue(GetKey(bucketName, objectKey), out var fileInfo))
        {
            return Result<Stream>.Failure(new CacheNotFoundError(bucketName, objectKey));
        }

        await fileInfo.WaitCompleteAsync();

        return fileInfo.OpenRead();
    }

    public async Task<Result> SaveStreamAsync(string bucketName, string objectKey, Stream s3Stream, PipeWriter response, CancellationToken ct = default)
    {
        var header = new FileHeader
        {
            Queue = 0,
            ReadingCount = 0,
            Reserved = 0,
        };

        var fileInfo = new SimpleInfo(_simpleQueueSettings.DataPath)
        {
            Bucket = bucketName,
            ObjectKey = objectKey,
            State = FileState.Caching,
            Header = header,
        };

        if (!_cachedFiles.TryAdd(GetKey(bucketName, objectKey), fileInfo))
        {
            return new CacheAlredyAddedError(bucketName, objectKey);
        }

        _cacheQueue.Enqueue(fileInfo);

        using (var fw = fileInfo.CreateWrite())
        {
            Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<FileHeader>()];
            MemoryMarshal.Write(buffer, in header);

            await fw.WriteAsync(buffer.ToArray().AsMemory(0, buffer.Length), ct);
            await s3Stream.CopyToAsync(fw, response, ct: ct);
        }

        fileInfo.SetComplete();

        return new Result();
    }

    private string GetKey(string bucketName, string objectKey)
    {
        return Path.Combine(_simpleQueueSettings.DataPath, bucketName, objectKey);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cleanupTask = CleanupAsync();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stopped = true;

        return Task.CompletedTask;
    }

    private async Task CleanupAsync()
    {
        while(!_stopped)
        {
            try
            {
                await Task.Delay(10);
                CleanupInternal();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cleanup error.");
            }
        }
    }

    private void CleanupInternal()
    {
        while (_cacheQueue.Count > _simpleQueueSettings.MaxCount)
        {
            if (_cacheQueue.TryDequeue(out var info))
            {
                _cachedFiles.Remove(GetKey(info.Bucket, info.ObjectKey), out _);
                info.Remove();
            }
        }
    }
}
