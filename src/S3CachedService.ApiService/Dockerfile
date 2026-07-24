FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY src/S3CachedService.ApiService/S3CachedService.ApiService.csproj src/S3CachedService.ApiService/
COPY src/S3CachedService.ServiceDefaults/S3CachedService.ServiceDefaults.csproj src/S3CachedService.ServiceDefaults/
RUN dotnet restore src/S3CachedService.ApiService/S3CachedService.ApiService.csproj

COPY src/ src/
RUN dotnet publish src/S3CachedService.ApiService/S3CachedService.ApiService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "S3CachedService.ApiService.dll"]
