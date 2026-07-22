using Amazon.S3;
using S3CachedService.ApiService.S3Client;
using OpenTelemetry.Trace;
using S3CachedService.ApiService.Cache;
using S3CachedService.ApiService.Cache.SimpleQueue;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder => tracerProviderBuilder
        .AddSource("AWS") // Источник от AWS SDK
        .AddAspNetCoreInstrumentation()
        .AddAWSInstrumentation());

builder.Services.AddSingleton<IAmazonS3>(p =>
{
    var accessKey = builder.Configuration["AWS:AccessKey"];
    var secretKey = builder.Configuration["AWS:SecretKey"];
    var config = new AmazonS3Config
    {
        ServiceURL = builder.Configuration["AWS:S3:ServiceURL"],
        ForcePathStyle = builder.Configuration.GetSection("AWS:S3:ForcePathStyle").Get<bool>(),
        UseHttp = builder.Configuration.GetSection("AWS:S3:UseHttp").Get<bool>(),
    };

    return new Amazon.S3.AmazonS3Client(accessKey, secretKey, config);
});

builder.Services.AddTransient<IS3Client, S3CachedService.ApiService.S3Client.AmazonS3Client>();
builder.Services.AddSingleton<ICachedFileService, CachedFileService>();
builder.Services.AddSingleton<IFileCache, SimpleFileCache>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.MapGet("/{*path}", GetFileAsync);

app.Run();

Task GetFileAsync(HttpContext httpContext, ICachedFileService cachedFileService)
{
    return cachedFileService.GetFileAsync(httpContext);
}
