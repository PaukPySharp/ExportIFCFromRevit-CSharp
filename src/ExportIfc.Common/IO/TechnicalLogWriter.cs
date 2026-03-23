using System.Text;

using ExportIfc.Config;

namespace ExportIfc.IO;

/// <summary>
/// Низкоуровневая запись технических текстовых логов.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Дописывает строки в текстовые технические логи add-in и оркестратора.
/// 2. Создаёт каталог целевого файла перед записью, если путь содержит каталог.
/// 3. Поддерживает запись как одиночных строк, так и форматированных блоков.
///
/// Контракты:
/// 1. Класс ожидает путь к файлу технического лога с каталогом назначения.
/// 2. Если путь не содержит каталога, запись в файл пропускается и best-effort сообщается в stderr.
/// 3. Ошибки фактической записи файла не скрываются.
/// 4. Текст записывается в UTF-8 без BOM.
/// </remarks>
public static class TechnicalLogWriter
{
    /// <summary>
    /// Дописывает одну строку в технический лог.
    /// </summary>
    /// <param name="fullPath">Путь к файлу технического лога с каталогом назначения.</param>
    /// <param name="line">Строка для записи.</param>
    /// <remarks>
    /// Если путь не содержит каталога, запись в файл пропускается.
    /// </remarks>
    public static void AppendLine(string fullPath, string line)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            TryReportSkippedWrite(fullPath);
            return;
        }

        Directory.CreateDirectory(directory);
        File.AppendAllLines(fullPath, [line], ProjectEncodings.Utf8NoBom);
    }

    /// <summary>
    /// Дописывает форматированный блок в технический лог.
    /// </summary>
    /// <param name="fullPath">Путь к файлу технического лога с каталогом назначения.</param>
    /// <param name="header">Заголовок блока.</param>
    /// <param name="lines">Строки содержимого блока.</param>
    /// <param name="useBullets">
    /// <see langword="true"/>, если непустые строки блока нужно записывать с маркером списка;
    /// иначе строки пишутся как есть.
    /// </param>
    /// <remarks>
    /// Если путь не содержит каталога, запись в файл пропускается.
    /// </remarks>
    public static void AppendBlock(
        string fullPath,
        string header,
        IEnumerable<string> lines,
        bool useBullets)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            TryReportSkippedWrite(fullPath);
            return;
        }

        Directory.CreateDirectory(directory);

        var buffer = new List<string>();

        if (File.Exists(fullPath) && new FileInfo(fullPath).Length > 0)
            buffer.Add(string.Empty);

        buffer.Add(new string('=', AddinLogSchema.BlockSeparatorLength));
        buffer.Add(header);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            buffer.Add(useBullets
                ? $"  - {line.Trim()}"
                : line);
        }

        File.AppendAllLines(fullPath, buffer, ProjectEncodings.Utf8NoBom);
    }

    /// <summary>
    /// Пытается сообщить о пропуске записи технического лога,
    /// если путь не содержит каталога назначения.
    /// </summary>
    /// <param name="fullPath">Исходный путь, переданный в метод записи.</param>
    /// <remarks>
    /// Для аварийного сообщения используется stderr напрямую,
    /// чтобы низкоуровневая запись техлога не зависела от основного logging pipeline.
    /// </remarks>
    private static void TryReportSkippedWrite(string fullPath)
    {
        try
        {
            Console.Error.WriteLine(
                $"[TechnicalLogWriter] Пропущена запись техлога: путь не содержит каталог. Path='{fullPath}'.");
        }
        catch
        {
            // Ошибка аварийного сообщения не должна влиять на основной прогон.
        }
    }
}
