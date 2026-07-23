using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using S3CachedService.ApiService.Errors;
using S3CachedService.ApiService.S3Client;

namespace S3CachedService.ApiService.Cache.SimpleQueue;

public class SimpleFileCache : IFileCache, IHostedService
{
    private readonly SimpleQueueSettings _simpleQueueSettings;

    private readonly ConcurrentDictionary<string, SimpleInfo> _cachedFiles;
    private readonly ConcurrentQueue<SimpleInfo> _cacheQueue;
    private readonly ILogger<SimpleFileCache> _logger;

    private Task? _cleanupTask;
    private bool _stopped;
    private long _totalBytes;

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

    /// <summary>
    /// Суммарный объём полезной нагрузки файлов в кэше (без заголовков).
    /// Значение приближённое: при вытеснении ещё докачивающегося файла возможен дрейф.
    /// </summary>
    public long TotalBytes => Interlocked.Read(ref _totalBytes);

    public async Task<Result<Stream>> GetFileAsync(string bucketName, string objectKey, RequestParameters parameters, CancellationToken ct = default)
    {
        if (!_cachedFiles.TryGetValue(GetKey(bucketName, objectKey), out var fileInfo))
        {
            return Result<Stream>.Failure(new CacheNotFoundError(bucketName, objectKey));
        }

        await fileInfo.WaitCompleteAsync();

        return fileInfo.OpenRead();
    }

    public Task<Result> SaveStreamAsync(string bucketName, string objectKey, S3ObjectStream s3Stream, PipeWriter response, CancellationToken ct = default)
    {
        return SaveStreamInternalAsync(bucketName, objectKey, s3Stream, response, responseRange: null, ct);
    }

    public Task<Result> SaveStreamAsync(string bucketName, string objectKey, S3ObjectStream s3Stream, PipeWriter response, ByteRange responseRange, CancellationToken ct = default)
    {
        return SaveStreamInternalAsync(bucketName, objectKey, s3Stream, response, responseRange, ct);
    }

    private async Task<Result> SaveStreamInternalAsync(string bucketName, string objectKey, S3ObjectStream s3Stream, PipeWriter response, ByteRange? responseRange, CancellationToken ct)
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
            ObjectSize = s3Stream.Length
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

            if (responseRange is null)
            {
                await s3Stream.Stream.CopyToAsync(fw, response, ct: ct);
            }
            else
            {
                await s3Stream.Stream.CopyToAsync(fw, response, responseRange.Value, ct: ct);
            }

            fileInfo.ObjectSize = (int)(fw.Length - Unsafe.SizeOf<FileHeader>());
        }

        Interlocked.Add(ref _totalBytes, fileInfo.ObjectSize);

        fileInfo.SetComplete();

        return new Result();
    }

    private string GetKey(string bucketName, string objectKey)
    {
        return Path.Combine(_simpleQueueSettings.DataPath, bucketName, objectKey);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RestoreCacheAsync(cancellationToken);

        _cleanupTask = CleanupAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stopped = true;

        return Task.CompletedTask;
    }

    private async Task RestoreCacheAsync(CancellationToken ct)
    {
        var restored = new List<SimpleInfo>();

        foreach (var filePath in Directory.EnumerateFiles(_simpleQueueSettings.DataPath, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var fileInfo = await TryRestoreFileAsync(filePath, ct);

            if (fileInfo != null)
            {
                restored.Add(fileInfo);
            }
        }

        foreach (var fileInfo in restored.OrderByDescending(i => i.Header.ReadingCount))
        {
            if (_cachedFiles.TryAdd(GetKey(fileInfo.Bucket, fileInfo.ObjectKey), fileInfo))
            {
                _cacheQueue.Enqueue(fileInfo);
                Interlocked.Add(ref _totalBytes, fileInfo.ObjectSize);
            }
        }

        _logger.LogInformation(
            "Cache restored from '{DataPath}': {Count} files, {TotalBytes} bytes.",
            _simpleQueueSettings.DataPath, _cacheQueue.Count, TotalBytes);
    }

    private async Task<SimpleInfo?> TryRestoreFileAsync(string filePath, CancellationToken ct)
    {
        // Ключ кэша строится из objectKey с разделителями '/' (как в URL запроса),
        // поэтому относительный путь нормализуется обратно к '/'.
        var relativePath = Path.GetRelativePath(_simpleQueueSettings.DataPath, filePath)
            .Replace(Path.DirectorySeparatorChar, '/');
        var separatorIndex = relativePath.IndexOf('/');

        if (separatorIndex <= 0 || separatorIndex == relativePath.Length - 1)
        {
            _logger.LogWarning("Skip cache file outside of a bucket folder: {Path}", filePath);
            return null;
        }

        var headerBuffer = new byte[Unsafe.SizeOf<FileHeader>()];
        long fileLength;

        await using (var fs = File.OpenRead(filePath))
        {
            fileLength = fs.Length;

            if (fileLength < headerBuffer.Length)
            {
                _logger.LogWarning("Skip cache file shorter than the header: {Path}", filePath);
                return null;
            }

            await fs.ReadExactlyAsync(headerBuffer, ct);
        }

        var header = MemoryMarshal.Read<FileHeader>(headerBuffer);

        if (header.TypeLetter1 != 'C' || header.TypeLetter2 != 'H' || header.TypeLetter3 != 'E')
        {
            _logger.LogWarning("Skip cache file with an invalid header: {Path}", filePath);
            return null;
        }

        var fileInfo = new SimpleInfo(_simpleQueueSettings.DataPath)
        {
            Bucket = relativePath[..separatorIndex],
            ObjectKey = relativePath[(separatorIndex + 1)..],
            ObjectSize = (int)(fileLength - headerBuffer.Length),
            Header = header,
        };

        fileInfo.SetComplete();

        return fileInfo;
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

    internal void CleanupInternal()
    {
        while (_cacheQueue.Count > _simpleQueueSettings.MaxCount
            || Interlocked.Read(ref _totalBytes) > _simpleQueueSettings.MaxBytes)
        {
            if (!_cacheQueue.TryDequeue(out var info))
            {
                // Очередь пуста, но счётчик ещё выше лимита (допустимый дрейф) —
                // выходим, иначе цикл никогда не завершится.
                break;
            }

            _cachedFiles.Remove(GetKey(info.Bucket, info.ObjectKey), out _);
            // Счётчик корректируется до удаления файла: File.Delete может бросить
            // исключение, а учёт всё равно должен сойтись.
            Interlocked.Add(ref _totalBytes, -info.ObjectSize);
            info.Remove();
        }
    }
}
