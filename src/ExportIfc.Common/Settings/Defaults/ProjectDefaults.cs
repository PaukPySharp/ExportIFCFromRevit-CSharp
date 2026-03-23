using ExportIfc.Config;

namespace ExportIfc.Settings.Defaults;

/// <summary>
/// Дефолтные значения проекта, используемые при отсутствии явной настройки.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует fallback-значения для загрузки <see cref="AppSettings"/>.
/// 2. Убирает размазывание проектных дефолтов между loader-классами и контейнерами настроек.
/// 3. Даёт одно место, где видно, чем именно заполняются пропущенные ini-параметры.
///
/// Контракты:
/// 1. Здесь хранятся именно проектные значения по умолчанию, а не вычисляемые пути.
/// 2. Эти значения применяются на этапе чтения <see cref="ProjectFileNames.SettingsIni"/>.
/// 3. Изменение значений этого класса влияет на поведение загрузки настроек,
///    но не меняет уже сохранённые ini-файлы.
/// </remarks>
public static class ProjectDefaults
{
    /// <summary>
    /// Имя листа Excel с основной таблицей путей.
    /// </summary>
    public const string SheetPath = "Path";

    /// <summary>
    /// Имя листа Excel со списком исключений.
    /// </summary>
    public const string SheetIgnore = "IgnoreList";

    /// <summary>
    /// Имя листа Excel с рабочей историей состояний RVT-моделей.
    /// </summary>
    public const string SheetHistory = "History";

    /// <summary>
    /// Имя 3D-вида Revit по умолчанию для экспорта IFC.
    /// </summary>
    public const string RevitExportView3dName = "Navisworks";

    /// <summary>
    /// Имя подпапки с общими JSON-конфигурациями по умолчанию.
    /// </summary>
    public const string MappingDirCommon = "00_Common";

    /// <summary>
    /// Имя подпапки с файлами сопоставления слоёв по умолчанию.
    /// </summary>
    public const string MappingDirLayers = "01_Export_Layers";

    /// <summary>
    /// Таймаут batch-процесса Revit по умолчанию.
    /// </summary>
    /// <remarks>
    /// Значение 0 означает отсутствие ограничения по времени.
    /// </remarks>
    public const int RevitBatchTimeoutMinutes = 0;
}