using System.IO.Pipelines;
using S3CachedService.ApiService.Cache;

public static class StreamExtensions
{
    public static async Task CopyToPipeAsync(this Stream source, PipeWriter destination, int bufferSize = 81920, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));
        ArgumentNullException.ThrowIfNull(destination, nameof(destination));

        var buffer = new byte[bufferSize];

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, ct);

            if (bytesRead <= 0)
            {
                break;
            }

            // Получаем буфер у PipeWriter'а
            var writeBuffer = destination.GetMemory(bytesRead);
            buffer.AsSpan(0, bytesRead).CopyTo(writeBuffer.Span);
            destination.Advance(bytesRead);

            // Уведомляем PipeWriter, что порция данных готова
            var result = await destination.FlushAsync(ct);

            if (result.IsCompleted)
            {
                break;
            }
        }

        // Завершаем запись
        destination.Complete();
    }

    public static async Task CopyToPipeAsync(this Stream source, PipeWriter destination, long maxBytes, int bufferSize = 81920, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));
        ArgumentNullException.ThrowIfNull(destination, nameof(destination));

        var buffer = new byte[bufferSize];
        var remaining = maxBytes;

        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var bytesRead = await source.ReadAsync(buffer.AsMemory(0, toRead), ct);

            if (bytesRead <= 0)
            {
                break;
            }

            var writeBuffer = destination.GetMemory(bytesRead);
            buffer.AsSpan(0, bytesRead).CopyTo(writeBuffer.Span);
            destination.Advance(bytesRead);
            remaining -= bytesRead;

            var result = await destination.FlushAsync(ct);

            if (result.IsCompleted)
            {
                break;
            }
        }

        destination.Complete();
    }

    public static async Task CopyToAsync(
        this Stream source,
        Stream destination,
        PipeWriter pipeWriter,
        int bufferSize = 81920,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));
        ArgumentNullException.ThrowIfNull(destination, nameof(destination));
        ArgumentNullException.ThrowIfNull(destination, nameof(pipeWriter));

        var buffer = new byte[bufferSize];
        var memory = buffer.AsMemory();

        while (true)
        {
            // Чтение из исходного потока
            var read = await source.ReadAsync(memory, ct);

            if (read == 0)
            {
                break;
            }

            // Запись в целевой поток
            await destination.WriteAsync(memory[..read], ct);

            // Запись в PipeWriter
            var writeBuffer = pipeWriter.GetMemory(read);
            memory[..read].CopyTo(writeBuffer);
            pipeWriter.Advance(read);

            var result = await pipeWriter.FlushAsync(ct);

            if (result.IsCompleted || ct.IsCancellationRequested)
            {
                break;
            }
        }

        // Завершение работы
        destination.Flush();
        await pipeWriter.CompleteAsync();
    }

    public static async Task CopyToAsync(
        this Stream source,
        Stream destination,
        PipeWriter pipeWriter,
        ByteRange pipeRange,
        int bufferSize = 81920,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));
        ArgumentNullException.ThrowIfNull(destination, nameof(destination));
        ArgumentNullException.ThrowIfNull(pipeWriter, nameof(pipeWriter));

        var buffer = new byte[bufferSize];
        var memory = buffer.AsMemory();
        long position = 0;

        while (true)
        {
            // Чтение из исходного потока
            var read = await source.ReadAsync(memory, ct);

            if (read == 0)
            {
                break;
            }

            // Запись в целевой поток — всегда целиком
            await destination.WriteAsync(memory[..read], ct);

            // В PipeWriter — только пересечение прочитанного куска с окном диапазона
            var chunkFrom = Math.Max(position, pipeRange.From);
            var chunkTo = Math.Min(position + read - 1, pipeRange.To);
            position += read;

            if (chunkFrom > chunkTo)
            {
                continue;
            }

            var offset = (int)(chunkFrom - (position - read));
            var count = (int)(chunkTo - chunkFrom + 1);

            var writeBuffer = pipeWriter.GetMemory(count);
            memory.Slice(offset, count).CopyTo(writeBuffer);
            pipeWriter.Advance(count);

            var result = await pipeWriter.FlushAsync(ct);

            if (result.IsCompleted || ct.IsCancellationRequested)
            {
                break;
            }
        }

        // Завершение работы
        destination.Flush();
        await pipeWriter.CompleteAsync();
    }
}