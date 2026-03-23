using ExportIfc.Config;

namespace ExportIfc.Settings;

/// <summary>
/// Настройки приложения, загружаемые из <see cref="ProjectFileNames.SettingsIni"/>.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Хранит уже подготовленные значения конфигурации приложения.
/// 2. Даёт единый объект настроек для оркестратора, add-in и вспомогательных сервисов.
/// 3. Отделяет хранение готовых значений от чтения ini-файла и разбора строк.
///
/// Контракты:
/// 1. Производные пути не вычисляются здесь.
/// 2. Значения по умолчанию выбираются на этапе загрузки настроек,
///    а не внутри этого класса.
/// 3. Экземпляр считается полностью подготовленным после создания через
///    <see cref="Loading.AppSettingsLoader"/>.
/// 4. Путь <see cref="DirAdminData"/> хранится уже в итоговом виде
///    для текущего режима запуска.
/// 5. Список версий Revit хранится как отдельная копия
///    и не должен меняться извне после создания объекта.
/// </remarks>
public sealed class AppSettings
{
    /// <summary>
    /// Создаёт экземпляр подготовленных настроек приложения.
    /// </summary>
    /// <param name="dirExportConfig">Путь к каталогу с конфигурациями экспорта.</param>
    /// <param name="dirAdminData">
    /// Итоговый путь к каталогу <see cref="ProjectDirectoryNames.AdminData"/>
    /// для текущего режима запуска.
    /// </param>
    /// <param name="mappingDirCommon">Имя подпапки с общими JSON-конфигурациями.</param>
    /// <param name="mappingDirLayers">Имя подпапки с файлами сопоставления категорий.</param>
    /// <param name="isProdMode">Признак production-режима.</param>
    /// <param name="runRevit">Признак запуска внешнего процесса Revit.</param>
    /// <param name="enableUnmappedExport">Признак включённой выгрузки IFC без маппинга.</param>
    /// <param name="revitBatchTimeoutMinutes">Таймаут ожидания batch-процесса Revit в минутах.</param>
    /// <param name="configJsonName">Базовое имя JSON-файла конфигурации.</param>
    /// <param name="sheetPath">Имя листа Excel с основной таблицей путей.</param>
    /// <param name="sheetIgnore">Имя листа Excel со списком исключений.</param>
    /// <param name="sheetHistory">Имя листа Excel с историей.</param>
    /// <param name="revitExportView3dName">Имя 3D-вида для экспорта IFC.</param>
    /// <param name="revitVersions">Список поддерживаемых версий Revit.</param>
    /// <remarks>
    /// Конструктор принимает уже подготовленные значения.
    /// Чтение ini-файла, выбор значений по умолчанию и приведение строк к типам
    /// выполняются до создания этого объекта.
    /// </remarks>
    internal AppSettings(
        string dirExportConfig,
        string dirAdminData,
        string mappingDirCommon,
        string mappingDirLayers,
        bool isProdMode,
        bool runRevit,
        bool enableUnmappedExport,
        int revitBatchTimeoutMinutes,
        string configJsonName,
        string sheetPath,
        string sheetIgnore,
        string sheetHistory,
        string revitExportView3dName,
        IReadOnlyList<int> revitVersions)
    {
        DirExportConfig = dirExportConfig;
        DirAdminData = dirAdminData;
        MappingDirCommon = mappingDirCommon;
        MappingDirLayers = mappingDirLayers;
        IsProdMode = isProdMode;
        RunRevit = runRevit;
        EnableUnmappedExport = enableUnmappedExport;
        RevitBatchTimeoutMinutes = revitBatchTimeoutMinutes;
        ConfigJsonName = configJsonName;
        SheetPath = sheetPath;
        SheetIgnore = sheetIgnore;
        SheetHistory = sheetHistory;
        RevitExportView3dName = revitExportView3dName;
        RevitVersions = revitVersions.ToArray();
    }

    /// <summary>
    /// Путь к каталогу с конфигурациями экспорта.
    /// </summary>
    public string DirExportConfig { get; }

    /// <summary>
    /// Итоговый путь к каталогу <see cref="ProjectDirectoryNames.AdminData"/>
    /// для текущего режима запуска.
    /// </summary>
    /// <remarks>
    /// В production-режиме путь берётся из параметра
    /// <see cref="ExportIfc.Settings.Schema.SettingsIniKeys.DirAdminData"/>.
    /// В непроизводственном режиме итоговый путь вычисляется
    /// относительно расположения <see cref="ProjectFileNames.SettingsIni"/>:
    /// если ini-файл лежит в каталоге <see cref="ProjectDirectoryNames.Settings"/>,
    /// используется его родительская директория; иначе используется
    /// каталог самого ini-файла.
    /// </remarks>
    public string DirAdminData { get; }

    /// <summary>
    /// Имя подпапки с общими JSON-конфигурациями.
    /// </summary>
    public string MappingDirCommon { get; }

    /// <summary>
    /// Имя подпапки с файлами сопоставления категорий.
    /// </summary>
    public string MappingDirLayers { get; }

    /// <summary>
    /// Признак production-режима.
    /// </summary>
    public bool IsProdMode { get; }

    /// <summary>
    /// Признак запуска внешнего процесса Revit.
    /// </summary>
    public bool RunRevit { get; }

    /// <summary>
    /// Признак включённой выгрузки IFC без маппинга.
    /// </summary>
    public bool EnableUnmappedExport { get; }

    /// <summary>
    /// Таймаут ожидания batch-процесса Revit в минутах.
    /// </summary>
    /// <remarks>
    /// Значение 0 или меньше отключает ограничение по времени.
    /// </remarks>
    public int RevitBatchTimeoutMinutes { get; }

    /// <summary>
    /// Базовое имя JSON-файла конфигурации.
    /// </summary>
    public string ConfigJsonName { get; }

    /// <summary>
    /// Имя листа Excel с основной таблицей путей.
    /// </summary>
    public string SheetPath { get; }

    /// <summary>
    /// Имя листа Excel со списком исключений.
    /// </summary>
    public string SheetIgnore { get; }

    /// <summary>
    /// Имя листа Excel с историей.
    /// </summary>
    public string SheetHistory { get; }

    /// <summary>
    /// Имя 3D-вида для экспорта IFC.
    /// </summary>
    public string RevitExportView3dName { get; }

    /// <summary>
    /// Список поддерживаемых версий Revit.
    /// </summary>
    /// <remarks>
    /// При штатной загрузке через <see cref="Loading.AppSettingsLoader"/>
    /// список уже очищен от дублей и отсортирован.
    /// </remarks>
    public IReadOnlyList<int> RevitVersions { get; }
}
