namespace S3CachedService.AppHost.MinIO;

public class MinioResource : ContainerResource, IResourceWithServiceDiscovery
{
    private const string DEFAULT_USER = "admin";

    public MinioResource(string name, ParameterResource? admin, ParameterResource adminPassword)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(adminPassword);

        AdminUserNameParameter = admin;
        AdminPasswordParameter = adminPassword;
    }

    /// <summary>
    /// Gets the parameter that contains the Minio admin.
    /// </summary>
    public ParameterResource? AdminUserNameParameter { get; }

    internal ReferenceExpression AdminReference =>
        AdminUserNameParameter is not null ?
            ReferenceExpression.Create($"{AdminUserNameParameter}") :
            ReferenceExpression.Create($"{DEFAULT_USER}");

    /// <summary>
    /// Gets the parameter that contains the Minio admin password.
    /// </summary>
    public ParameterResource AdminPasswordParameter { get; }
}
