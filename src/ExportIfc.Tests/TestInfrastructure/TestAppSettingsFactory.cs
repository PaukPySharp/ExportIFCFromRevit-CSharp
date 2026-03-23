using ExportIfc.Settings;

namespace ExportIfc.Tests.TestInfrastructure;

/// <summary>
/// Фабрика типовых <see cref="AppSettings"/> для тестов оркестратора.
/// </summary>
/// <remarks>
/// Helper возвращает уже нормализованный набор итоговых значений.
/// Логика вычисления runtime-путей и prod/non-prod выбора остаётся в основном коде,
/// а не дублируется в тестовой инфраструктуре.
/// </remarks>
internal static class TestAppSettingsFactory
{
    /// <summary>
    /// Создаёт типовой набор настроек для тестов.
    /// </summary>
    /// <param name="root">Корень временного тестового окружения.</param>
    /// <param name="dirExportConfig">Явный путь к каталогу export-config.</param>
    /// <param name="dirAdminData">Явный путь к каталогу admin_data.</param>
    /// <param name="mappingDirCommon">Имя подпапки общих JSON-конфигураций.</param>
    /// <param name="mappingDirLayers">Имя подпапки файлов сопоставления категорий Revit и классов IFC.</param>
    /// <param name="isProdMode">Признак production-режима.</param>
    /// <param name="runRevit">Признак запуска внешнего Revit.</param>
    /// <param name="enableUnmappedExport">Признак включённой выгрузки без маппинга.</param>
    /// <param name="revitBatchTimeoutMinutes">Таймаут ожидания batch-процесса Revit.</param>
    /// <param name="configJsonName">Базовое имя JSON-конфигурации IFC.</param>
    /// <param name="sheetPath">Имя листа Path.</param>
    /// <param name="sheetIgnore">Имя листа Ignore.</param>
    /// <param name="sheetHistory">Имя листа History.</param>
    /// <param name="revitExportView3dName">Имя 3D-вида для экспорта IFC.</param>
    /// <param name="revitVersions">Список поддерживаемых версий Revit.</param>
    /// <returns>Подготовленный экземпляр <see cref="AppSettings"/>.</returns>
    public static AppSettings Create(
        string root,
        string? dirExportConfig = null,
        string? dirAdminData = null,
        string mappingDirCommon = "00_Common",
        string mappingDirLayers = "01_Layers",
        bool isProdMode = false,
        bool runRevit = true,
        bool enableUnmappedExport = false,
        int revitBatchTimeoutMinutes = 0,
        string configJsonName = "Export_Settings",
        string sheetPath = "Path",
        string sheetIgnore = "IgnoreList",
        string sheetHistory = "History",
        string revitExportView3dName = "Navisworks",
        IReadOnlyList<int>? revitVersions = null)
    {
        return new AppSettings(
            dirExportConfig: dirExportConfig ?? Path.Combine(root, "export-config"),
            dirAdminData: dirAdminData ?? Path.Combine(root, "admin_data"),
            mappingDirCommon: mappingDirCommon,
            mappingDirLayers: mappingDirLayers,
            isProdMode: isProdMode,
            runRevit: runRevit,
            enableUnmappedExport: enableUnmappedExport,
            revitBatchTimeoutMinutes: revitBatchTimeoutMinutes,
            configJsonName: configJsonName,
            sheetPath: sheetPath,
            sheetIgnore: sheetIgnore,
            sheetHistory: sheetHistory,
            revitExportView3dName: revitExportView3dName,
            revitVersions: revitVersions ?? [2022, 2023]);
    }
}
