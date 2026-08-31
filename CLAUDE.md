# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Что это

**s3-cacher** (солюшен `S3CachedService.slnx`) — кэширующий HTTP-прокси перед S3-хранилищем.
Единственный эндпоинт `GET /{bucket}/{objectKey...}` отдаёт файл из локального дискового кэша,
а при промахе — стримит его из S3, параллельно записывая в кэш (tee: один проход по потоку
пишет и в ответ клиенту, и в файл кэша).

.NET 9 + .NET Aspire 9.0. Основная разработка идёт в `master` (фичи — через
короткоживущие ветки и PR). В `README.md` — конфигурация для DevOps
(env-переменные, пример манифеста Kubernetes) и примеры запросов.

## Команды

```powershell
dotnet build S3CachedService.slnx
dotnet test                                   # xUnit, поднимает весь AppHost (нужен Docker)
dotnet test --filter FullyQualifiedName~WebTests.GetWebResourceRootReturnsOkStatusCode

# Полный стек через Aspire (MinIO в Docker + ApiService + Web):
dotnet run --project src/S3CachedService.AppHost
# Требует секреты-параметры MinIO (user secrets AppHost):
dotnet user-secrets set Parameters:minio-username <user> --project src/S3CachedService.AppHost
dotnet user-secrets set Parameters:minio-password <pass> --project src/S3CachedService.AppHost

# ApiService отдельно (S3 по адресу из appsettings: http://localhost:9000, ключи AWS:AccessKey/SecretKey):
dotnet run --project src/S3CachedService.ApiService
```

## Архитектура

Поток запроса: `Program.cs` (catch-all `MapGet("/{*path}")`) → `CachedFileService.GetFileAsync`
→ сначала `IFileCache.GetFileAsync`, при `CacheNotFoundError` — `IS3Client.GetObjectStreamAsync`
и `IFileCache.SaveStreamAsync` (пишет в кэш и в `PipeWriter` ответа одновременно,
см. `StreamExtensions`).

- **`Cache/`** — абстракция кэша `IFileCache` и две реализации: `SimpleQueue/SimpleFileCache`
  (рабочая) и `NotFileCache` (заглушка-пассивный прокси без кэширования, подменяется в DI).
- **`SimpleQueue/`** — кэш на диске: `ConcurrentDictionary` (индекс) + `ConcurrentQueue`
  (порядок вытеснения FIFO). Настройки `SimpleQueueSettings` (`DataPath`, `MaxCount`,
  `MaxBytes`) привязаны к секции конфигурации `QueueSettings` в `Program.cs`. Файлы лежат
  в `DataPath` (по умолчанию `CacheData/`) как `{bucket}/{objectKey}`, в начало файла
  пишется бинарный `FileHeader` (при чтении делается `Seek` на его размер).
  `SimpleInfo.WaitCompleteAsync` позволяет параллельным читателям дождаться конца
  записи файла в кэш. `SimpleFileCache` зарегистрирован и как `IHostedService`:
  фоновая чистка `CleanupAsync` вытесняет файлы по FIFO при превышении `MaxCount`
  или `MaxBytes`.
- **`S3Client/`** — `IS3Client` поверх `AWSSDK.S3` (`AmazonS3Client`), ошибки AWS
  транслируются в доменные через `S3ErrorExtensions.HandleError`.
- **`Errors/` + `Result`/`Result<T>`** — ошибки не бросаются, а возвращаются как значения:
  иерархия `ServiceError` (каждая знает свой HTTP-код через `GetDetails()` и сама пишет
  JSON в ответ через `WriteToAsync`). Новый тип ошибки = наследник `ServiceError`.
- **`AppHost/MinIO/`** — самописный Aspire-ресурс MinIO (`AddMinio(...)`: контейнер
  `quay.io/minio/minio`, порты 9000/9001, volume `minio-files`).
- **`Web/`** — Blazor-фронтенд, пока нетронутый шаблон Aspire (Counter/Weather).
- **`ServiceDefaults/`** — стандартный Aspire-проект (OpenTelemetry, health checks,
  service discovery); ApiService дополнительно трейсит AWS SDK.

## Особенности и известные шероховатости

- **Кодировка**: файлы с русскими комментариями хранятся в UTF-8 с BOM. Исторически
  часть файлов была в Windows-1251 (исправлено); новые файлы с кириллицей сохранять
  в UTF-8, иначе комментарии будут читаться как мусор.
- Значения по умолчанию в коде `SimpleQueueSettings` (1 000 000 файлов, 500 МБ)
  отличаются от заданных в `appsettings.json` (1 000 файлов, 100 МБ) — эффективны
  вторые, если секцию `QueueSettings` не переопределили env-переменными.
- `ShouldDisplayInBrowser` определяет `Content-Disposition: inline` по белому списку
  MIME-типов; имя файла для клиента можно переопределить query-параметром `?fileName=`.
