using System.IO.Pipelines;

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
}