using System.IO.Pipelines;

namespace S3CachedService.ApiService.Cache;

public interface IFileCache
{
    Task<Result<Stream>> GetFileAsync(string bucketName, string objectKey, RequestParameters parameters, CancellationToken ct = default);

    Task<Result> SaveStreamAsync(string bucketName, string objectKey, Stream s3Stream, PipeWriter response, CancellationToken ct = default);
}
