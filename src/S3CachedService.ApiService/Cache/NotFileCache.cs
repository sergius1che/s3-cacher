using System.IO.Pipelines;
using S3CachedService.ApiService.Errors;
using S3CachedService.ApiService.S3Client;

namespace S3CachedService.ApiService.Cache;

public class NotFileCache : IFileCache
{
    public Task<Result<Stream>> GetFileAsync(string bucketName, string objectKey, RequestParameters parameters, CancellationToken ct = default)
    {
        return Task.FromResult(Result<Stream>.Failure(new CacheNotFoundError(bucketName, objectKey)));
    }

    public async Task<Result> SaveStreamAsync(string bucketName, string objectKey, S3ObjectStream s3Stream, PipeWriter response, CancellationToken ct = default)
    {
        return new Result();
    }

    public async Task<Result> SaveStreamAsync(string bucketName, string objectKey, S3ObjectStream s3Stream, PipeWriter response, ByteRange responseRange, CancellationToken ct = default)
    {
        return new Result();
    }
}
