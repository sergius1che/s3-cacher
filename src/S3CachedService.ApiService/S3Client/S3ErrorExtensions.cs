using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using S3CachedService.ApiService.Errors;

namespace S3CachedService.ApiService.S3Client;

public static class S3ErrorExtensions
{
    public static ServiceError HandleError(this AmazonS3Exception ex, string bucketName, string key)
    {
        return ex.StatusCode switch
        {
            HttpStatusCode.NotFound => new S3ObjectNotFoundError(bucketName, key),
            HttpStatusCode.Forbidden => new S3AccessDeniedError(),
            HttpStatusCode.InternalServerError => new S3InternalServerError($"Error over get {bucketName}/{key} with message: {ex.Message}"),
            _ => new S3ClientError(ex.Message),
        };
    }
}
