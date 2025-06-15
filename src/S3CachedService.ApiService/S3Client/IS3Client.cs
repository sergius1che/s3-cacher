namespace S3CachedService.ApiService.S3Client;

public interface IS3Client
{
    Task<Result<Stream>> GetObjectStreamAsync(string bucketName, string objectKey, CancellationToken ct = default);

    Task<Result<byte[]>> GetObjectBytesAsync(string bucketName, string objectKey, CancellationToken ct = default);

    Task<Result<string>> GetObjectAsStringAsync(string bucketName, string objectKey, CancellationToken ct = default);

    Task<Result<bool>> DoesObjectExistAsync(string bucketName, string objectKey, CancellationToken ct = default);

    Task<Result> UploadObjectFromStreamAsync(string bucketName, string objectKey, Stream data, string contentType, CancellationToken ct = default);

    Task<Result> DeleteObjectAsync(string bucketName, string objectKey, CancellationToken ct = default);
}
