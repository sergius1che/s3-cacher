using System.Net;
using Amazon.S3;
using Amazon.S3.Model;

namespace S3CachedService.ApiService.S3Client;

/// <summary>
/// Client fo connect to S3 storage over Amazon client
/// </summary>
public class AmazonS3Client : IS3Client
{
    private readonly IAmazonS3 _s3Client;

    public AmazonS3Client(IAmazonS3 s3Client)
    {
        _s3Client = s3Client;
    }

    /// <inheritdoc/>
    public async Task<Result<S3ObjectStream>> GetObjectStreamAsync(string bucketName, string objectKey, CancellationToken ct = default)
    {
        var request = new GetObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey
        };

        try
        {
            var response = await _s3Client.GetObjectAsync(request, ct);

            return new S3ObjectStream(response.ResponseStream, response.ContentLength);
        }
        catch (AmazonS3Exception ex)
        {
            return Result<S3ObjectStream>.Failure(ex.HandleError(bucketName, objectKey));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<byte[]>> GetObjectBytesAsync(string bucketName, string objectKey, CancellationToken ct = default)
    {
        var result = await GetObjectStreamAsync(bucketName, objectKey);

        if (!result.IsSuccess)
        {
            return Result<byte[]>.Failure(result.Error);
        }

        using var s3Object = result.Value;
        using var memoryStream = new MemoryStream();
        await s3Object.Stream.CopyToAsync(memoryStream, ct);

        return memoryStream.ToArray();
    }

    /// <inheritdoc/>
    public async Task<Result<string>> GetObjectAsStringAsync(string bucketName, string objectKey, CancellationToken ct = default)
    {
        var result = await GetObjectStreamAsync(bucketName, objectKey, ct);

        if (!result.IsSuccess)
        {
            return Result<string>.Failure(result.Error);
        }

        using var s3Object = result.Value;
        using var reader = new StreamReader(s3Object.Stream);

        return await reader.ReadToEndAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> DoesObjectExistAsync(string bucketName, string objectKey, CancellationToken ct = default)
    {
        var request = new GetObjectMetadataRequest
        {
            BucketName = bucketName,
            Key = objectKey
        };

        try
        {
            var meta = await _s3Client.GetObjectMetadataAsync(request, ct);

            return meta.HttpStatusCode == HttpStatusCode.OK;
        }
        catch (AmazonS3Exception ex)
        {
            return Result<bool>.Failure(ex.HandleError(bucketName, objectKey));
        }
    }

    /// <inheritdoc/>
    public async Task<Result> UploadObjectFromStreamAsync(string bucketName, string objectKey, Stream data, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            InputStream = data,
            ContentType = contentType
        };

        try
        {
            await _s3Client.PutObjectAsync(request, ct);

            return new Result();
        }
        catch (AmazonS3Exception ex)
        {
            return ex.HandleError(bucketName, objectKey);
        }
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteObjectAsync(string bucketName, string objectKey, CancellationToken ct = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey
        };

        try
        {
            await _s3Client.DeleteObjectAsync(request, ct);

            return new Result();
        }
        catch (AmazonS3Exception ex)
        {
            return ex.HandleError(bucketName, objectKey);
        }
    }
}
