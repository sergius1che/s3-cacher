namespace S3CachedService.ApiService.Cache.SimpleQueue;

internal enum FileState
{
    None = 0,
    Caching = 1,
    Complete = 2,
}
