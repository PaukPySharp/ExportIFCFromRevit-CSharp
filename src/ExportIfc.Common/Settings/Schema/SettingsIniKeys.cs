namespace ExportIfc.Settings.Schema;

/// <summary>
/// Ключи параметров <see cref="Config.ProjectFileNames.SettingsIni"/>.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует имена параметров ini-файла и составные ключи для <c>IConfiguration</c>.
/// 2. Убирает размазывание строковых литералов по загрузчику, тестам и сообщениям об ошибках.
/// 3. Даёт единый источник истины для ini-контракта.
///
/// Контракты:
/// 1. Суффикс <c>Name</c> означает имя параметра внутри секции.
/// 2. Поля без суффикса <c>Name</c> — это составные ключи формата <c>Section:Key</c>.
/// 3. Изменение значений меняет контракт чтения <see cref="Config.ProjectFileNames.SettingsIni"/>.
/// </remarks>
public static class SettingsIniKeys
{
    /// <summary>
    /// Имя параметра каталога конфигураций экспорта.
    /// </summary>
    public const string DirExportConfigName = "dir_export_config";

    /// <summary>
    /// Имя параметра каталога административных данных.
    /// </summary>
    public const string DirAdminDataName = "dir_admin_data";

    /// <summary>
    /// Имя параметра общей папки маппинга.
    /// </summary>
    public const string MappingDirCommonName = "dir_common";

    /// <summary>
    /// Имя параметра папки layer-маппинга.
    /// </summary>
    public const string MappingDirLayersName = "dir_layers";

    /// <summary>
    /// Имя параметра production-режима.
    /// </summary>
    public const string IsProdModeName = "is_prod_mode";

    /// <summary>
    /// Имя параметра запуска Revit.
    /// </summary>
    public const string RunRevitName = "run_revit";

    /// <summary>
    /// Имя параметра разрешения выгрузки без маппинга.
    /// </summary>
    public const string EnableUnmappedExportName = "enable_unmapped_export";

    /// <summary>
    /// Имя параметра таймаута batch-запуска Revit в минутах.
    /// </summary>
    public const string RevitBatchTimeoutMinutesName = "revit_batch_timeout_minutes";

    /// <summary>
    /// Имя параметра JSON-конфигурации.
    /// </summary>
    public const string ConfigJsonName = "config_json";

    /// <summary>
    /// Имя параметра листа с путями.
    /// </summary>
    public const string SheetPathName = "sheet_path";

    /// <summary>
    /// Имя параметра листа исключений.
    /// </summary>
    public const string SheetIgnoreName = "sheet_ignore";

    /// <summary>
    /// Имя параметра листа истории.
    /// </summary>
    public const string SheetHistoryName = "sheet_history";

    /// <summary>
    /// Имя параметра рабочего 3D-вида Revit для экспорта.
    /// </summary>
    public const string RevitExportView3dNameName = "export_view3d_name";

    /// <summary>
    /// Имя параметра списка поддерживаемых версий Revit.
    /// </summary>
    public const string RevitVersionsName = "revit_versions";

    /// <summary>
    /// Составной ключ каталога конфигураций экспорта.
    /// </summary>
    public const string DirExportConfig =
        SettingsIniSections.Paths + ":" + DirExportConfigName;

    /// <summary>
    /// Составной ключ каталога административных данных.
    /// </summary>
    public const string DirAdminData =
        SettingsIniSections.Paths + ":" + DirAdminDataName;

    /// <summary>
    /// Составной ключ общей папки маппинга.
    /// </summary>
    public const string MappingDirCommon =
        SettingsIniSections.Mapping + ":" + MappingDirCommonName;

    /// <summary>
    /// Составной ключ папки layer-маппинга.
    /// </summary>
    public const string MappingDirLayers =
        SettingsIniSections.Mapping + ":" + MappingDirLayersName;

    /// <summary>
    /// Составной ключ production-режима.
    /// </summary>
    public const string IsProdMode =
        SettingsIniSections.Settings + ":" + IsProdModeName;

    /// <summary>
    /// Составной ключ запуска Revit.
    /// </summary>
    public const string RunRevit =
        SettingsIniSections.Settings + ":" + RunRevitName;

    /// <summary>
    /// Составной ключ разрешения выгрузки без маппинга.
    /// </summary>
    public const string EnableUnmappedExport =
        SettingsIniSections.Settings + ":" + EnableUnmappedExportName;

    /// <summary>
    /// Составной ключ таймаута batch-запуска Revit в минутах.
    /// </summary>
    public const string RevitBatchTimeoutMinutes =
        SettingsIniSections.Settings + ":" + RevitBatchTimeoutMinutesName;

    /// <summary>
    /// Составной ключ JSON-конфигурации.
    /// </summary>
    public const string ConfigJson =
        SettingsIniSections.Files + ":" + ConfigJsonName;

    /// <summary>
    /// Составной ключ листа с путями.
    /// </summary>
    public const string SheetPath =
        SettingsIniSections.Excel + ":" + SheetPathName;

    /// <summary>
    /// Составной ключ листа исключений.
    /// </summary>
    public const string SheetIgnore =
        SettingsIniSections.Excel + ":" + SheetIgnoreName;

    /// <summary>
    /// Составной ключ листа истории.
    /// </summary>
    public const string SheetHistory =
        SettingsIniSections.Excel + ":" + SheetHistoryName;

    /// <summary>
    /// Составной ключ рабочего 3D-вида Revit для экспорта.
    /// </summary>
    public const string RevitExportView3dName =
        SettingsIniSections.Revit + ":" + RevitExportView3dNameName;

    /// <summary>
    /// Составной ключ списка поддерживаемых версий Revit.
    /// </summary>
    public const string RevitVersions =
        SettingsIniSections.Revit + ":" + RevitVersionsName;
}