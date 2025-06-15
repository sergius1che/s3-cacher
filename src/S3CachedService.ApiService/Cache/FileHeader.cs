namespace S3CachedService.ApiService.Cache;

public struct FileHeader
{
    public FileHeader()
    {
    }

    /// <summary>
    /// Always 'C'
    /// </summary>
    public char TypeLetter1 { get; set; } = 'C';

    /// <summary>
    /// Always 'H'
    /// </summary>
    public char TypeLetter2 { get; set; } = 'H';

    /// <summary>
    /// Always 'E'
    /// </summary>
    public char TypeLetter3 { get; set; } = 'E';

    /// <summary>
    /// Count of cache reading
    /// </summary>
    public uint ReadingCount { get; set; }

    /// <summary>
    /// Queue level
    /// </summary>
    public byte Queue { get; set; }

    /// <summary>
    /// Reserved for future
    /// </summary>
    public int Reserved { get; set; }

    public FileHeader Touch(uint count)
    {
        ReadingCount = count;

        return this;
    }
}
