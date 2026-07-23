namespace S3CachedService.ApiService.S3Client;

/// <summary>
/// Client fot S3 storage
/// </summary>
public interface IS3Client
{
    /// <summary>
    /// Get an object stream from storage
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="objectKey">Unique object identifier</param>
    /// <param name="ct">Operation cancellation token</param>
    /// <returns>Result with object <see cref="Stream"/></returns>
    Task<Result<Stream>> GetObjectStreamAsync(string bucketName, string objectKey, CancellationToken ct = default);

    /// <summary>
    /// Get an object bytes from storage
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="objectKey">Unique object identifier</param>
    /// <param name="ct">Operation cancellation token</param>
    /// <returns>Result with object <see cref="byte[]"/></returns>
    Task<Result<byte[]>> GetObjectBytesAsync(string bucketName, string objectKey, CancellationToken ct = default);

    /// <summary>
    /// Get an object as string from storage
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="objectKey">Unique object identifier</param>
    /// <param name="ct">Operation cancellation token</param>
    /// <returns>Result with object <see cref="string"/></returns>
    Task<Result<string>> GetObjectAsStringAsync(string bucketName, string objectKey, CancellationToken ct = default);

    /// <summary>
    /// Check object exists
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="objectKey">Unique object identifier</param>
    /// <param name="ct">Operation cancellation token</param>
    /// <returns>Result with <see cref="bool"/></returns>
    Task<Result<bool>> DoesObjectExistAsync(string bucketName, string objectKey, CancellationToken ct = default);

    /// <summary>
    /// Загрузка файла в файловое хранилище
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="objectKey">Unique object identifier</param>
    /// <param name="data">Object stream for uploading</param>
    /// <param name="contentType">Object mime type</param>
    /// <param name="ct">Operation cancellation token</param>
    /// <returns>Result operation</returns>
    Task<Result> UploadObjectFromStreamAsync(string bucketName, string objectKey, Stream data, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Удаление объекта из хранилища
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="objectKey">Unique object identifier</param>
    /// <param name="ct">Operation cancellation token</param>
    /// <returns>Result operation</returns>
    Task<Result> DeleteObjectAsync(string bucketName, string objectKey, CancellationToken ct = default);
}
