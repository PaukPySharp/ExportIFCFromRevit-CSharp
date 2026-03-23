using System.Text;

using ExportIfc.Config;

namespace ExportIfc.IO;

/// <summary>
/// Работа с текстовыми логами проекта.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Формирует пути к текстовым логам проекта.
/// 2. Дописывает строки в ежедневные txt-логи.
/// 3. Добавляет разделитель между логическими блоками записей.
///
/// Контракты:
/// 1. Датированное имя формируется как
///    &lt;baseName&gt;_&lt;date&gt;<see cref="ProjectFileExtensions.Txt"/>,
///    где формат <c>date</c> задан в <see cref="ProjectFormats.LogDate"/>.
/// 2. Если <c>baseName</c> уже содержит расширение
///    <see cref="ProjectFileExtensions.Txt"/>, суффикс даты добавляется перед расширением,
///    а не после него.
/// 3. <c>baseName</c> должен быть корректным именем файла без каталогов.
/// 4. Целевой каталог создаётся при первой записи.
/// 5. Если разделитель пустой, он не записывается.
/// 6. Разделитель в конец существующего лога добавляется только если
///    файл был изменён в момент запуска либо позже.
/// </remarks>
public static class TextLogs
{
    /// <summary>
    /// Стандартный разделитель блоков в txt-логах.
    /// </summary>
    public static readonly string LogSeparator = new('=', AddinLogSchema.BlockSeparatorLength);

    /// <summary>
    /// Формирует путь к txt-логу.
    /// </summary>
    /// <param name="dirLogs">Каталог логов.</param>
    /// <param name="baseName">Базовое имя файла без каталогов.</param>
    /// <param name="addDateSuffix">Нужно ли добавлять суффикс даты.</param>
    /// <param name="dateLocal">
    /// Дата для суффикса. Если не задана, используется текущее локальное время.
    /// </param>
    /// <returns>Полный путь к лог-файлу.</returns>
    /// <remarks>
    /// Метод нормализует <paramref name="baseName"/> как имя txt-файла проекта.
    /// Если имя уже содержит расширение <see cref="ProjectFileExtensions.Txt"/>,
    /// при добавлении суффикса даты расширение сохраняется в конце.
    /// </remarks>
    public static string BuildPath(
        string dirLogs,
        string baseName,
        bool addDateSuffix = true,
        DateTime? dateLocal = null)
    {
        var fileName = BuildFileName(baseName, addDateSuffix, dateLocal);
        return Path.Combine(dirLogs, fileName);
    }

    /// <summary>
    /// Дописывает строки в датированный лог и, при необходимости,
    /// завершает блок стандартным разделителем.
    /// </summary>
    /// <param name="dirLogs">Каталог логов.</param>
    /// <param name="baseName">Базовое имя файла без каталогов.</param>
    /// <param name="lines">Строки для записи.</param>
    public static void WriteLines(string dirLogs, string baseName, IEnumerable<string> lines)
        => WriteLines(dirLogs, baseName, lines, LogSeparator);

    /// <summary>
    /// Дописывает строки в датированный лог и, при необходимости,
    /// завершает блок указанным разделителем.
    /// </summary>
    /// <param name="dirLogs">Каталог логов.</param>
    /// <param name="baseName">Базовое имя файла без каталогов.</param>
    /// <param name="lines">Строки для записи.</param>
    /// <param name="separator">Разделитель в конце блока.</param>
    public static void WriteLines(
        string dirLogs,
        string baseName,
        IEnumerable<string> lines,
        string separator)
    {
        var logPath = BuildPath(dirLogs, baseName, addDateSuffix: true);
        AppendCore(logPath, lines, separator);
    }

    /// <summary>
    /// Добавляет разделитель в конец датированного лога,
    /// если файл существует и был затронут в текущем запуске.
    /// </summary>
    /// <param name="dirLogs">Каталог логов.</param>
    /// <param name="baseName">Базовое имя файла без каталогов.</param>
    /// <param name="minMtimeUnix">
    /// Минимальный Unix-time последней модификации файла.
    /// Если файл старше, разделитель не добавляется.
    /// </param>
    public static void AppendSeparatorIfTouched(
        string dirLogs,
        string baseName,
        long? minMtimeUnix)
        => AppendSeparatorIfTouched(
            dirLogs,
            baseName,
            LogSeparator,
            minMtimeUnix);

    /// <summary>
    /// Добавляет указанный разделитель в конец датированного лога,
    /// если файл существует и был затронут в текущем запуске.
    /// </summary>
    /// <param name="dirLogs">Каталог логов.</param>
    /// <param name="baseName">Базовое имя файла без каталогов.</param>
    /// <param name="separator">Строка-разделитель.</param>
    /// <param name="minMtimeUnix">
    /// Минимальный Unix-time последней модификации файла.
    /// Если файл старше, разделитель не добавляется.
    /// </param>
    public static void AppendSeparatorIfTouched(
        string dirLogs,
        string baseName,
        string separator,
        long? minMtimeUnix)
    {
        if (string.IsNullOrEmpty(separator))
            return;

        var path = BuildPath(dirLogs, baseName, addDateSuffix: true);
        if (!File.Exists(path))
            return;

        if (minMtimeUnix is not null)
        {
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
            var unixTime = new DateTimeOffset(lastWriteTimeUtc).ToUnixTimeSeconds();
            if (unixTime < minMtimeUnix.Value)
                return;
        }

        File.AppendAllText(path, separator + Environment.NewLine, ProjectEncodings.Utf8NoBom);
    }

    /// <summary>
    /// Строит имя txt-лога без каталога.
    /// </summary>
    /// <param name="baseName">Базовое имя файла без каталогов.</param>
    /// <param name="addDateSuffix">Нужно ли добавлять суффикс даты.</param>
    /// <param name="dateLocal">Дата для суффикса.</param>
    /// <returns>Имя файла без каталога.</returns>
    /// <remarks>
    /// Метод валидирует <paramref name="baseName"/> как имя файла проекта
    /// и не допускает передачи каталогов, недопустимых символов
    /// и некорректных системных имён.
    /// </remarks>
    private static string BuildFileName(
        string baseName,
        bool addDateSuffix,
        DateTime? dateLocal)
    {
        var normalizedFileName = FileSystemEx.TryNormalizeFileName(
            baseName,
            ProjectFileExtensions.Txt)
            ?? throw new ArgumentException(
                "Ожидается корректное имя txt-лога без каталогов.",
                nameof(baseName));

        if (!addDateSuffix)
            return normalizedFileName;

        var stem = Path.GetFileNameWithoutExtension(normalizedFileName);
        var date = (dateLocal ?? DateTime.Now).ToString(ProjectFormats.LogDate);

        return stem + "_" + date + ProjectFileExtensions.Txt;
    }

    /// <summary>
    /// Выполняет фактическую дозапись строк в лог-файл.
    /// </summary>
    /// <param name="fullPath">Полный путь к файлу.</param>
    /// <param name="lines">Строки для записи.</param>
    /// <param name="separator">Разделитель в конце блока.</param>
    /// <remarks>
    /// Метод создаёт каталог файла при необходимости и пишет строки как один логический блок.
    /// </remarks>
    private static void AppendCore(
        string fullPath,
        IEnumerable<string> lines,
        string separator)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        using var writer = new StreamWriter(fullPath, append: true, ProjectEncodings.Utf8NoBom);

        foreach (var line in lines)
            writer.WriteLine(NormalizeLine(line));

        if (!string.IsNullOrEmpty(separator))
            writer.WriteLine(separator);
    }

    /// <summary>
    /// Нормализует строку перед записью в txt-лог.
    /// </summary>
    /// <param name="raw">Исходная строка.</param>
    /// <returns>Строка без завершающих пробелов и переводов строки.</returns>
    private static string NormalizeLine(string? raw)
        => (raw ?? string.Empty).TrimEnd();
}
