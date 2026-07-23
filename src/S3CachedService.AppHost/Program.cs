using S3CachedService.AppHost.MinIO;

var builder = DistributedApplication.CreateBuilder(args);

var minioUsername = builder.AddParameter("minio-username", secret: true);
var minioPassword = builder.AddParameter("minio-password", secret: true);
var minio = builder
    .AddMinio("minio", 9000, 9001, minioUsername, minioPassword)
    .WithVolume("minio-files", "/data", false);

var apiService = builder.AddProject<Projects.S3CachedService_ApiService>("apiservice")
    .WaitFor(minio);

builder.AddProject<Projects.S3CachedService_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
