using ExportIfc.Settings;
using ExportIfc.Settings.Schema;

namespace ExportIfc.Config;

/// <summary>
/// Производные пути и рабочие каталоги проекта.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Формирует абсолютные пути к ключевым каталогам приложения
///    на основе уже загруженных настроек.
/// 2. Централизует вычисление рабочих директорий проекта.
/// 3. Разводит вычисляемые пути и простые строковые имена каталогов.
///
/// Контракты:
/// 1. Здесь не создаются каталоги — только вычисляются пути.
/// 2. Путь <see cref="DirAdminData"/> приходит уже в итоговом виде
///    из <see cref="AppSettings"/>.
/// 3. Каталоги <see cref="SettingsIniKeys.DirExportConfigName"/> и
///    <see cref="SettingsIniKeys.DirAdminDataName"/> должны существовать.
/// 4. Формулы производных каталогов берутся из <see cref="ProjectDirectories"/>.
/// </remarks>
public sealed class ProjectPaths
{
    /// <summary>
    /// Создаёт набор рабочих путей проекта.
    /// </summary>
    /// <param name="dirExportConfig">Каталог конфигураций экспорта.</param>
    /// <param name="dirAdminData">Каталог административных данных.</param>
    /// <param name="dirLogs">Каталог txt-логов.</param>
    /// <param name="dirTechLogs">Каталог технических логов add-in.</param>
    /// <param name="dirHistory">Каталог истории.</param>
    private ProjectPaths(
        string dirExportConfig,
        string dirAdminData,
        string dirLogs,
        string dirTechLogs,
        string dirHistory)
    {
        DirExportConfig = dirExportConfig;
        DirAdminData = dirAdminData;
        DirLogs = dirLogs;
        DirTechLogs = dirTechLogs;
        DirHistory = dirHistory;
    }

    /// <summary>
    /// Каталог с конфигурациями экспорта.
    /// </summary>
    public string DirExportConfig { get; }

    /// <summary>
    /// Базовый каталог <see cref="ProjectDirectoryNames.AdminData"/>.
    /// </summary>
    public string DirAdminData { get; }

    /// <summary>
    /// Каталог логов внутри <see cref="ProjectDirectoryNames.AdminData"/>.
    /// </summary>
    public string DirLogs { get; }

    /// <summary>
    /// Каталог технических логов внутри <see cref="DirLogs"/>.
    /// </summary>
    public string DirTechLogs { get; }

    /// <summary>
    /// Каталог истории внутри <see cref="ProjectDirectoryNames.AdminData"/>.
    /// </summary>
    public string DirHistory { get; }

    /// <summary>
    /// Строит набор рабочих путей проекта.
    /// </summary>
    /// <param name="stg">Загруженные настройки приложения.</param>
    /// <returns>Нормализованный набор путей проекта.</returns>
    public static ProjectPaths Build(AppSettings stg)
    {
        if (stg is null)
            throw new ArgumentNullException(nameof(stg));

        var dirExportConfig = RequireDirectory(
            NormalizeDirectory(stg.DirExportConfig),
            SettingsIniKeys.DirExportConfigName);

        var dirAdminData = RequireDirectory(
            NormalizeDirectory(stg.DirAdminData),
            SettingsIniKeys.DirAdminDataName);

        var dirLogs = ProjectDirectories.Logs(dirAdminData);
        var dirTechLogs = ProjectDirectories.TechLogs(dirAdminData);
        var dirHistory = ProjectDirectories.History(dirAdminData);

        return new ProjectPaths(
            dirExportConfig: dirExportConfig,
            dirAdminData: dirAdminData,
            dirLogs: dirLogs,
            dirTechLogs: dirTechLogs,
            dirHistory: dirHistory);
    }

    /// <summary>
    /// Нормализует путь к каталогу.
    /// </summary>
    /// <param name="path">Исходный путь.</param>
    /// <returns>Полный путь без завершающего разделителя каталога.</returns>
    private static string NormalizeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Получен пустой путь к каталогу.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Проверяет, что путь указывает на существующий каталог.
    /// </summary>
    /// <param name="path">Нормализованный путь к каталогу.</param>
    /// <param name="pathName">Имя пути для текста ошибки.</param>
    /// <returns>Тот же путь, если каталог существует.</returns>
    /// <exception cref="IOException">
    /// Выбрасывается, если путь указывает на файл вместо каталога.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Выбрасывается, если каталог не существует.
    /// </exception>
    private static string RequireDirectory(string path, string pathName)
    {
        if (Directory.Exists(path))
            return path;

        if (File.Exists(path))
        {
            throw new IOException(
                $"Для каталога '{pathName}' получен путь = {path}, но это файл. Ожидалась папка.");
        }

        throw new DirectoryNotFoundException(
            $"Для каталога '{pathName}' получен путь = {path}, но такой директории не существует. " +
            $"Проверь {ProjectFileNames.SettingsIni}.");
    }
}