namespace S3CachedService.ApiService.Cache.SimpleQueue;

public class SimpleQueueSettings
{
    public string DataPath { get; set; } = "CacheData";

    public int MaxCount { get; set; } = 1_000_000;

    public long MaxBytes { get; set; } = 1024L * 1024L * 500L; // 500 MB
}
