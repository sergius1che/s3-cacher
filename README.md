# S3 cacher

Кэширующий HTTP-прокси перед S3-хранилищем. Единственный эндпоинт
`GET /{bucket}/{objectKey...}` отдаёт файл из локального дискового кэша, а при
промахе — стримит его из S3, параллельно записывая в кэш (один проход по потоку
пишет и в ответ клиенту, и в файл кэша).

Подробности архитектуры и команды разработчика — в [CLAUDE.md](CLAUDE.md).

## Конфигурация (для DevOps)

Приложение — ASP.NET Core (.NET 9): конфигурация читается из `appsettings.json`
и переменных окружения (иерархия ключей кодируется через `__`).

Ключи, которые читает `S3CachedService.ApiService`:

| Ключ | Переменная окружения | Назначение |
|------|----------------------|------------|
| `AWS:AccessKey` | `AWS__AccessKey` | Access key S3 |
| `AWS:SecretKey` | `AWS__SecretKey` | Secret key S3 |
| `AWS:S3:ServiceURL` | `AWS__S3__ServiceURL` | Адрес S3-эндпоинта (MinIO и т.п.) |
| `AWS:S3:ForcePathStyle` | `AWS__S3__ForcePathStyle` | Path-style адресация бакетов (для MinIO — `true`) |
| `AWS:S3:UseHttp` | `AWS__S3__UseHttp` | Ходить в S3 по HTTP вместо HTTPS |
| `QueueSettings:DataPath` | `QueueSettings__DataPath` | Папка дискового кэша (в `appsettings.json` — `CacheData`) |
| `QueueSettings:MaxCount` | `QueueSettings__MaxCount` | Максимум файлов в кэше (в `appsettings.json` — `1000`) |
| `QueueSettings:MaxBytes` | `QueueSettings__MaxBytes` | Максимальный суммарный размер кэша в байтах (в `appsettings.json` — `104857600`, т.е. 100 МБ) |

При превышении любого из лимитов фоновая чистка вытесняет файлы из кэша
в порядке FIFO.

Пример конфига приложения:

```yaml
    configmap:
      ASPNETCORE_ENVIRONMENT: "Stage"
      # S3 хранилище
      AWS__AccessKey: "SimpleAccessKey"
      AWS__SecretKey: "SimpleSecretKey"
      AWS__S3__ServiceURL: "http://minio.storage.svc:9000/"
      AWS__S3__ForcePathStyle: "true"
      AWS__S3__UseHttp: "true"
      # Дисковый кэш
      QueueSettings__DataPath: "/var/cache/s3-cacher"
      QueueSettings__MaxCount: "10000"
      QueueSettings__MaxBytes: "1073741824"
```

Замечания:

- **Кэш пишется на диск** в папку из `QueueSettings__DataPath` (в
  `appsettings.json` — `CacheData` в рабочей директории приложения) —
  подмонтируйте туда volume. При постоянном (persistent) томе кэш переживает
  рестарт: индекс восстанавливается при старте сканированием папки.
- **Значения по умолчанию**: если секция `QueueSettings` не задана вовсе,
  действуют значения из кода (`CacheData`, 1 000 000 файлов, 500 МБ); но в
  поставляемом `appsettings.json` секция задана — `CacheData`, 1 000 файлов,
  100 МБ.
- **Health-эндпоинты** `/health` и `/alive`
  настроить, пока это не изменено в `ServiceDefaults`.
- Восстановление индекса кэша выполняется в `IHostedService.StartAsync` **до**
  привязки порта: на большом кэше старт занимает заметное время — закладывайте
  его в startup probe.

## Пример запроса

Получить объект `reports/2026/report.pdf` из бакета `documents`:

```bash
curl -v http://localhost:8080/documents/reports/2026/report.pdf -o report.pdf
```

Повторный запрос того же пути отдаётся уже из дискового кэша, без похода в S3.

Query-параметр `fileName` переопределяет имя файла в `Content-Disposition`
(имя, которое клиент увидит при скачивании):

```bash
curl -v "http://localhost:8080/documents/reports/2026/report.pdf?fileName=annual-report.pdf" -O -J
```

Ошибки возвращаются в JSON. Например, `404` для отсутствующего объекта:

```json
{
  "httpCode": 404,
  "title": "Object not found",
  "details": "Object 'reports/2026/missing.pdf' not found in bucket 'documents'."
}
```
