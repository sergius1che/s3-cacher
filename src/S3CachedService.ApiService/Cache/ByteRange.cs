namespace S3CachedService.ApiService.Cache;

/// <summary>
/// Absolute byte window of an object payload, both bounds inclusive
/// </summary>
public readonly record struct ByteRange(long From, long To)
{
    /// <summary>
    /// Window length in bytes
    /// </summary>
    public long Length => To - From + 1;
}
