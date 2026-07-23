namespace S3CachedService.ApiService.S3Client;

/// <summary>
/// S3 object content stream with the full object length
/// </summary>
public sealed class S3ObjectStream : IDisposable
{
    public S3ObjectStream(Stream stream, long length)
    {
        Stream = stream;
        Length = length;
    }

    /// <summary>
    /// Object content stream
    /// </summary>
    public Stream Stream { get; }

    /// <summary>
    /// Full object length in bytes
    /// </summary>
    public long Length { get; }

    public void Dispose()
    {
        Stream.Dispose();
    }
}
