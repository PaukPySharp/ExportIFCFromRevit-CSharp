using System.Diagnostics;

using ExportIfc.IO;
using ExportIfc.Config;

namespace ExportIfc.Settings.Loading;

/// <summary>
/// Поиск и разрешение пути к <see cref="ProjectFileNames.SettingsIni"/>.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует правила поиска ini-файла для всех сценариев запуска.
/// 2. Даёт единый контракт для стартового процесса и дочернего запуска Revit.
/// 3. Убирает дублирование и снижает риск расхождения путей между процессами.
///
/// Приоритет источников:
/// 1. Явный путь из аргумента командной строки.
/// 2. Явный путь из переменной окружения <see cref="EnvironmentVariableNames.SettingsIni"/>.
/// 3. Автообнаружение файла по типовым стартовым каталогам.
/// 4. Предсказуемый путь-кандидат в текущей рабочей директории.
///
/// Важный контракт:
/// Если путь был явно передан аргументом или через переменную окружения,
/// он считается источником истины даже тогда, когда файл не существует.
/// Это позволяет не маскировать ошибку конфигурации случайным поиском
/// другого <see cref="ProjectFileNames.SettingsIni"/> где-то выше по дереву папок.
/// </remarks>
public static class SettingsIniLocator
{
    /// <summary>
    /// Разрешает путь к ini-файлу на старте внешнего оркестратора.
    /// </summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <returns>Полный путь к ini-файлу.</returns>
    public static string ResolveStartupPath(string[]? args)
    {
        var commandLinePath = TryGetCommandLinePath(args);
        if (commandLinePath is not null)
            return commandLinePath;

        var environmentPath = TryGetEnvironmentPath();
        if (environmentPath is not null)
            return environmentPath;

        return ResolveByDiscovery();
    }

    /// <summary>
    /// Разрешает путь к ini-файлу для дочернего процесса Revit.
    /// </summary>
    /// <returns>Полный путь к ini-файлу.</returns>
    /// <remarks>
    /// В штатном сценарии путь должен уже прийти через
    /// <see cref="EnvironmentVariableNames.SettingsIni"/>.
    /// Если переменная окружения отсутствует, применяется то же автообнаружение,
    /// что и на старте оркестратора, чтобы не плодить разные правила поиска.
    /// </remarks>
    public static string ResolveForChildProcess()
    {
        var environmentPath = TryGetEnvironmentPath();
        if (environmentPath is not null)
            return environmentPath;

        return ResolveByDiscovery();
    }

    /// <summary>
    /// Пробует получить путь из первого аргумента командной строки.
    /// </summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <returns>Нормализованный полный путь или <see langword="null"/>.</returns>
    private static string? TryGetCommandLinePath(string[]? args)
    {
        if (args is null || args.Length == 0)
            return null;

        return TryNormalizeConfiguredPath(
            args[0],
            "аргумента командной строки");
    }

    /// <summary>
    /// Пробует получить путь из переменной окружения проекта.
    /// </summary>
    /// <returns>Нормализованный полный путь или <see langword="null"/>.</returns>
    private static string? TryGetEnvironmentPath()
    {
        return TryNormalizeConfiguredPath(
            Environment.GetEnvironmentVariable(EnvironmentVariableNames.SettingsIni),
            $"переменной окружения {EnvironmentVariableNames.SettingsIni}");
    }

    /// <summary>
    /// Нормализует явно переданный путь к ini-файлу.
    /// </summary>
    /// <param name="rawPath">Исходная строка пути.</param>
    /// <param name="sourceName">Человекочитаемое описание источника пути.</param>
    /// <returns>Полный путь или <see langword="null"/>, если значение пустое.</returns>
    /// <exception cref="ArgumentException">
    /// Выбрасывается, если из явного источника пришёл синтаксически некорректный путь.
    /// </exception>
    private static string? TryNormalizeConfiguredPath(string? rawPath, string sourceName)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return null;

        var trimmedPath = rawPath?.Trim();

        try
        {
            return Path.GetFullPath(trimmedPath);
        }
        catch (Exception ex) when (
            ex is ArgumentException ||
            ex is NotSupportedException ||
            ex is PathTooLongException)
        {
            throw new ArgumentException(
                $"Получен некорректный путь к {ProjectFileNames.SettingsIni} из {sourceName}: '{rawPath}'.",
                ex);
        }
    }

    /// <summary>
    /// Выполняет автообнаружение ini-файла по набору типовых стартовых каталогов.
    /// </summary>
    /// <returns>Полный путь к найденному файлу или fallback-кандидат.</returns>
    private static string ResolveByDiscovery()
    {
        foreach (var searchRoot in EnumerateSearchRoots())
        {
            var found = FileSystemEx.FindFileUpwards(
                searchRoot,
                ProjectRelativePaths.SettingsIni);

            if (!string.IsNullOrWhiteSpace(found))
                return Path.GetFullPath(found);
        }

        return BuildDefaultCandidatePath();
    }

    /// <summary>
    /// Возвращает набор стартовых каталогов для поиска ini-файла.
    /// </summary>
    /// <returns>Последовательность нормализованных директорий без дублей.</returns>
    /// <remarks>
    /// Порядок важен:
    /// 1. Текущая рабочая директория процесса.
    /// 2. Базовая директория приложения.
    /// 3. Директория основного исполняемого файла.
    /// </remarks>
    private static IEnumerable<string> EnumerateSearchRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var searchRootCandidates = new[]
                 {
                     Environment.CurrentDirectory,
                     AppContext.BaseDirectory,
                     TryGetProcessDirectory()
                 };

        foreach (var candidate in searchRootCandidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch
            {
                continue;
            }

            if (seen.Add(fullPath))
                yield return fullPath;
        }
    }

    /// <summary>
    /// Формирует детерминированный fallback-путь, если автообнаружение ничего не нашло.
    /// </summary>
    /// <returns>Полный путь-кандидат к ini-файлу.</returns>
    private static string BuildDefaultCandidatePath()
    {
        var baseDirectory = string.IsNullOrWhiteSpace(Environment.CurrentDirectory)
            ? AppContext.BaseDirectory
            : Environment.CurrentDirectory;

        return Path.GetFullPath(
            Path.Combine(
                baseDirectory,
                ProjectRelativePaths.SettingsIni));
    }

    /// <summary>
    /// Пробует определить директорию основного исполняемого файла процесса.
    /// </summary>
    /// <returns>Полный путь к директории процесса или <see langword="null"/>.</returns>
    private static string? TryGetProcessDirectory()
    {
        try
        {
            var processPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(processPath))
                return null;

            return Path.GetDirectoryName(processPath);
        }
        catch
        {
            // В некоторых окружениях доступ к MainModule ограничен.
            // Для разрешения файла конфигурации это не критично — просто исключаем
            // этот источник из цепочки автообнаружения.
            return null;
        }
    }
}