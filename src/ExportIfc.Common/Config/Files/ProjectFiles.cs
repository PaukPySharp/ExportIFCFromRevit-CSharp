using ExportIfc.IO;
using ExportIfc.Settings;

namespace ExportIfc.Config;

/// <summary>
/// Файлы проекта и правила построения их абсолютных путей.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Формирует абсолютные пути к ключевым файлам проекта.
/// 2. Централизует правила именования служебных файлов.
/// 3. Разводит файловые контракты и вычисляемые каталоги по разным модулям.
///
/// Контракты:
/// 1. Имя JSON-конфигурации берётся из настроек, расширение добавляется здесь.
/// 2. Каталоги и файлы в этом модуле не создаются.
/// 3. Вычисление каталогов вынесено в <see cref="ProjectDirectories"/>.
/// 4. Технические логи в каталоге _tech формируются без суффикса даты.
/// </remarks>
public static class ProjectFiles
{
    /// <summary>
    /// Возвращает имя JSON-конфигурации с гарантированным расширением.
    /// </summary>
    /// <param name="stg">Загруженные настройки приложения.</param>
    /// <returns>Имя JSON-файла конфигурации.</returns>
    public static string JsonConfigFileName(AppSettings stg)
        => FileSystemEx.EnsureExtension(stg.ConfigJsonName, ProjectFileExtensions.Json);

    /// <summary>
    /// Возвращает путь к книге <see cref="ProjectFileNames.ManageWorkbook"/>.
    /// </summary>
    /// <param name="paths">Рабочие пути проекта.</param>
    /// <returns>Полный путь к управляющей Excel-книге.</returns>
    public static string ManageXlsx(ProjectPaths paths)
        => Path.Combine(paths.DirAdminData, ProjectFileNames.ManageWorkbook);

    /// <summary>
    /// Возвращает путь к книге <see cref="ProjectFileNames.HistoryWorkbook"/>.
    /// </summary>
    /// <param name="paths">Рабочие пути проекта.</param>
    /// <returns>Полный путь к книге истории.</returns>
    public static string HistoryXlsx(ProjectPaths paths)
        => Path.Combine(paths.DirHistory, ProjectFileNames.HistoryWorkbook);

    /// <summary>
    /// Возвращает имя файла списка моделей для указанной версии Revit.
    /// </summary>
    /// <param name="revitMajor">Major-версия Revit.</param>
    /// <returns>Имя Task-файла без каталога.</returns>
    public static string TaskFileName(int revitMajor)
        => ProjectFileNames.TaskFilePrefix + revitMajor + ProjectFileExtensions.Txt;

    /// <summary>
    /// Возвращает путь к файлу списка моделей для указанной версии Revit.
    /// </summary>
    /// <param name="paths">Рабочие пути проекта.</param>
    /// <param name="revitMajor">Major-версия Revit.</param>
    /// <returns>Полный путь к Task-файлу.</returns>
    public static string TaskFile(ProjectPaths paths, int revitMajor)
        => Path.Combine(paths.DirAdminData, TaskFileName(revitMajor));

    /// <summary>
    /// Возвращает путь к файлу <see cref="ProjectFileNames.TmpJson"/>
    /// внутри <see cref="ProjectDirectoryNames.AdminData"/>.
    /// </summary>
    /// <param name="paths">Рабочие пути проекта.</param>
    /// <returns>Полный путь к временному JSON-файлу передачи.</returns>
    public static string TmpJson(ProjectPaths paths)
        => TmpJson(paths.DirAdminData);

    /// <summary>
    /// Возвращает путь к файлу <see cref="ProjectFileNames.TmpJson"/>
    /// внутри <see cref="ProjectDirectoryNames.AdminData"/>.
    /// </summary>
    /// <param name="dirAdminData">Базовый каталог административных данных.</param>
    /// <returns>Полный путь к временному JSON-файлу передачи.</returns>
    public static string TmpJson(string dirAdminData)
        => Path.Combine(dirAdminData, ProjectFileNames.TmpJson);

    /// <summary>
    /// Возвращает имя dry-run JSON-файла пакета для указанной версии Revit.
    /// </summary>
    /// <param name="revitMajor">Major-версия Revit.</param>
    /// <returns>Имя debug JSON-файла пакета без каталога.</returns>
    public static string DryRunTransferJsonFileName(int revitMajor)
        => ProjectFileNames.TmpDryRunFilePrefix + revitMajor + ProjectFileExtensions.Json;

    /// <summary>
    /// Возвращает путь к dry-run JSON-файлу пакета для указанной версии Revit.
    /// </summary>
    /// <param name="paths">Рабочие пути проекта.</param>
    /// <param name="revitMajor">Major-версия Revit.</param>
    /// <returns>Полный путь к debug JSON-файлу dry-run пакета.</returns>
    public static string DryRunTransferJson(ProjectPaths paths, int revitMajor)
        => Path.Combine(paths.DirAdminData, DryRunTransferJsonFileName(revitMajor));

    /// <summary>
    /// Возвращает путь к txt-файлу зеркала консольного вывода оркестратора.
    /// </summary>
    /// <param name="dirAdminData">Базовый каталог административных данных.</param>
    /// <param name="runId">Идентификатор текущего запуска в формате <see cref="ProjectFormats.RunId"/>.</param>
    /// <returns>Полный путь к txt-файлу зеркала консоли.</returns>
    /// <remarks>
    /// Консольные зеркала выносятся в отдельную подпапку внутри <c>_tech</c>,
    /// чтобы не смешивать per-run консольный шум с постоянными техническими логами add-in.
    /// </remarks>
    public static string OrchestratorConsoleLog(string dirAdminData, string runId)
        => Path.Combine(
            ProjectDirectories.TechConsoleLogs(dirAdminData),
            FileSystemEx.EnsureExtension($"{runId}_{LogFiles.TechOrchestratorConsole}", ProjectFileExtensions.Txt));

    /// <summary>
    /// Возвращает путь к техническому логу старта add-in.
    /// </summary>
    /// <param name="dirAdminData">Базовый каталог административных данных.</param>
    /// <returns>Полный путь к txt-логу старта add-in.</returns>
    public static string AddinStartupLog(string dirAdminData)
        => Path.Combine(
            ProjectDirectories.TechLogs(dirAdminData),
            FileSystemEx.EnsureExtension(LogFiles.TechAddinStartup, ProjectFileExtensions.Txt));

    /// <summary>
    /// Возвращает путь к техническому логу фатальных ошибок add-in.
    /// </summary>
    /// <param name="dirAdminData">Базовый каталог административных данных.</param>
    /// <returns>Полный путь к txt-логу фатальных ошибок add-in.</returns>
    public static string AddinFatalLog(string dirAdminData)
        => Path.Combine(
            ProjectDirectories.TechLogs(dirAdminData),
            FileSystemEx.EnsureExtension(LogFiles.TechAddinFatal, ProjectFileExtensions.Txt));

    /// <summary>
    /// Возвращает путь к файлу журнала статусов add-in.
    /// </summary>
    /// <param name="dirAdminData">Базовый каталог административных данных.</param>
    /// <returns>Полный путь к txt-файлу статусов add-in.</returns>
    public static string AddinStatusFile(string dirAdminData)
        => Path.Combine(
            ProjectDirectories.TechLogs(dirAdminData),
            FileSystemEx.EnsureExtension(LogFiles.TechAddinStatus, ProjectFileExtensions.Txt));
}