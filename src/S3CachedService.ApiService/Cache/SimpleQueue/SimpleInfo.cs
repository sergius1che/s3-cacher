using System.Runtime.CompilerServices;

namespace S3CachedService.ApiService.Cache.SimpleQueue;

internal class SimpleInfo
{
    private static readonly int _headerLingth = Unsafe.SizeOf<FileHeader>();

    private readonly TaskCompletionSource<bool> _writeIsCompleted = new();
    private readonly string _rootPath;

    public SimpleInfo(string rootPath)
    {
        _rootPath = rootPath;
    }

    public required string Bucket { get; init; }

    public required string ObjectKey { get; init; }

    public long ObjectSize { get; set; }

    public FileState State { get; set; }

    public FileHeader Header { get; set; }

    public Task<bool> WaitCompleteAsync()
    {
        return _writeIsCompleted.Task;
    }

    public Stream CreateWrite()
    {
        var filePath = GetPath();
        var path = Path.GetDirectoryName(filePath);

        if (!Directory.Exists(path) && path != null)
        {
            Directory.CreateDirectory(path);
        }

        State = FileState.Caching;

        return File.Create(filePath, 80 * 1024, FileOptions.Asynchronous);
    }

    public Stream OpenRead()
    {
        var path = GetPath();

        var fs = File.OpenRead(path);

        fs.Seek(_headerLingth, SeekOrigin.Begin);

        return fs;
    }

    public void SetComplete()
    {
        State = FileState.Complete;

        _writeIsCompleted.SetResult(true);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as SimpleInfo);
    }

    public bool Equals(SimpleInfo? other)
    {
        return !ReferenceEquals(other, null) && Bucket == other.Bucket && ObjectKey == other.ObjectKey;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Bucket, ObjectKey);
    }

    public string GetPath()
    {
        return Path.Combine(_rootPath, Bucket, ObjectKey);
    }

    public void Remove()
    {
        var path = GetPath();

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
