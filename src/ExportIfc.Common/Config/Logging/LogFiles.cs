using ExportIfc.IO;

namespace ExportIfc.Config;

/// <summary>
/// Базовые имена txt-логов.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует имена лог-файлов проекта.
/// 2. Не допускает размазывания строковых констант по коду.
/// 3. Формирует безопасное имя лога для случая с отсутствующим 3D-видом.
///
/// Контракты:
/// 1. Ежедневные логи используются как базовые имена с датой.
/// 2. Технические логи в каталоге _tech используются без суффикса даты.
/// </remarks>
public static class LogFiles
{
    /// <summary>Лог ошибок при открытии моделей в Revit.</summary>
    public const string OpeningErrors = "1_opening_errors";

    /// <summary>Лог ошибок экспорта IFC.</summary>
    public const string ExportErrors = "2_export_errors";

    /// <summary>Лог случаев, когда не удалось определить версию Revit.</summary>
    public const string VersionNotFound = "3_revit_version_not_found";

    /// <summary>Лог моделей с версией Revit выше поддерживаемого диапазона.</summary>
    public const string VersionTooNew = "4_revit_version_too_new";

    /// <summary>Лог проблем с mtime моделей.</summary>
    public const string MTimeIssues = "5_mtime_issues";

    /// <summary>Технический лог-зеркало консольного вывода оркестратора.</summary>
    public const string TechOrchestratorConsole = "tech_orchestrator_console";

    /// <summary>Лог старта add-in.</summary>
    public const string TechAddinStartup = "tech_addin_startup";

    /// <summary>Лог фатальных исключений add-in.</summary>
    public const string TechAddinFatal = "tech_addin_fatal";

    /// <summary>Лог статусов add-in.</summary>
    public const string TechAddinStatus = "tech_addin_status";

    /// <summary>
    /// Формирует имя лога для моделей без нужного 3D-вида.
    /// </summary>
    /// <param name="viewName">Имя 3D-вида.</param>
    /// <returns>Безопасное базовое имя лог-файла.</returns>
    public static string MissingView(string viewName)
    {
        var safeViewName = string.IsNullOrWhiteSpace(viewName)
            ? string.Empty
            : FileSystemEx.SanitizeFileNamePart(viewName.Trim());

        return $"0_not_found_3dview_{safeViewName}";
    }
}