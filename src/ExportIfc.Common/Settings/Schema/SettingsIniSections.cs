namespace ExportIfc.Settings.Schema;

/// <summary>
/// Имена секций <see cref="Config.ProjectFileNames.SettingsIni"/>.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует имена секций ini-файла.
/// 2. Убирает строковые литералы вида "[Paths]" из кода и тестов.
/// 3. Делает структуру ini-контракта отдельно находимой.
///
/// Контракты:
/// 1. Значения должны совпадать с именами секций в <see cref="Config.ProjectFileNames.SettingsIni"/>.
/// 2. Здесь хранятся только имена секций без квадратных скобок.
/// </remarks>
public static class SettingsIniSections
{
    /// <summary>
    /// Секция путей проекта.
    /// </summary>
    public const string Paths = "Paths";

    /// <summary>
    /// Секция имён файлов и связанных файловых параметров.
    /// </summary>
    public const string Files = "Files";

    /// <summary>
    /// Секция общих флагов и параметров запуска.
    /// </summary>
    public const string Settings = "Settings";

    /// <summary>
    /// Секция параметров Revit.
    /// </summary>
    public const string Revit = "Revit";

    /// <summary>
    /// Секция параметров Excel.
    /// </summary>
    public const string Excel = "Excel";

    /// <summary>
    /// Секция параметров маппинга.
    /// </summary>
    public const string Mapping = "Mapping";
}