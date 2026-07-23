namespace S3CachedService.AppHost.MinIO;

public static class MinioAspireExtensions
{
    private const string ADMIN_ENV_VAR_NAME = "MINIO_ROOT_USER";
    private const string ADMIN_PASSWORD_ENV_VAR_NAME = "MINIO_ROOT_PASSWORD";
    private const string ADDRESS_ENV_VAR_NAME = "MINIO_ADDRESS";
    private const string CONSOLE_ADDRESS_ENV_VAR_NAME = "MINIO_CONSOLE_ADDRESS";

    private const int DEFAULT_CONTAINER_PORT = 9000;
    private const int DEFAULT_CONSOLE_PORT = 9001;

    public static IResourceBuilder<MinioResource> AddMinio(
        this IDistributedApplicationBuilder builder,
        string name,
        int? containerPort = null,
        int? consolePort = null,
        IResourceBuilder<ParameterResource>? adminUsername = null,
        IResourceBuilder<ParameterResource>? adminPassword = null)
    {
        var passwordParameter = adminPassword?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password");

        var resource = new MinioResource(name, adminUsername?.Resource, passwordParameter);

        var minio = builder
            .AddResource(resource)
            .WithImage("minio/minio")
            .WithImageRegistry("quay.io")
            .WithImageTag("RELEASE.2025-04-22T22-12-26Z")
            .WithHttpEndpoint(port: containerPort, targetPort: DEFAULT_CONTAINER_PORT, name: "minio-container-port")
            .WithHttpEndpoint(port: consolePort, targetPort: DEFAULT_CONSOLE_PORT, name: "minio-console-port")
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables[ADMIN_ENV_VAR_NAME] = resource.AdminReference;
                context.EnvironmentVariables[ADMIN_PASSWORD_ENV_VAR_NAME] = resource.AdminPasswordParameter;
                context.EnvironmentVariables[ADDRESS_ENV_VAR_NAME] = $":{DEFAULT_CONTAINER_PORT}";
                context.EnvironmentVariables[CONSOLE_ADDRESS_ENV_VAR_NAME] = $":{DEFAULT_CONSOLE_PORT}";
            });

        minio.WithArgs("server", "/data");

        return minio;
    }
}
