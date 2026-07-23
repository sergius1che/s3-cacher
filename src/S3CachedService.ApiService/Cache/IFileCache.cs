using System.IO.Pipelines;
using S3CachedService.ApiService.S3Client;

namespace S3CachedService.ApiService.Cache;

/// <summary>
/// Local cache of S3 objects
/// </summary>
public interface IFileCache
{
    /// <summary>
    /// Get a cached object stream
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="objectKey">Unique object identifier</param>
    /// <param name="parameters">Client request parameters</param>
    /// <param name="ct">Operation cancellation token</param>
    /// <returns>
    /// Result with object <see cref="Stream"/>;
    /// CacheNotFoundError when the object is not cached yet
    /// </returns>
    Task<Result<Stream>> GetFileAsync(string bucketName, string objectKey, RequestParameters parameters, CancellationToken ct = default);

    /// <summary>
    /// Save an object stream to the cache, simultaneously writing it to the client response
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="objectKey">Unique object identifier</param>
    /// <param name="s3Stream">Source object stream from S3</param>
    /// <param name="response">Client response writer receiving the same bytes</param>
    /// <param name="ct">Operation cancellation token</param>
    /// <returns>
    /// Result operation;
    /// CacheAlredyAddedError when the object is already being cached
    /// </returns>
    Task<Result> SaveStreamAsync(string bucketName, string objectKey, S3ObjectStream s3Stream, PipeWriter response, CancellationToken ct = default);

    /// <summary>
    /// Save a full object stream to the cache, writing only the requested byte window to the client response
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="objectKey">Unique object identifier</param>
    /// <param name="s3Stream">Source object stream from S3</param>
    /// <param name="response">Client response writer receiving only the window bytes</param>
    /// <param name="responseRange">Absolute byte window of the payload to write to the response</param>
    /// <param name="ct">Operation cancellation token</param>
    /// <returns>
    /// Result operation;
    /// CacheAlredyAddedError when the object is already being cached
    /// </returns>
    Task<Result> SaveStreamAsync(string bucketName, string objectKey, S3ObjectStream s3Stream, PipeWriter response, ByteRange responseRange, CancellationToken ct = default);
}
