using ExportIfc.IO;
using ExportIfc.Config;
using ExportIfc.Settings.Schema;
using ExportIfc.Settings.Defaults;

using Microsoft.Extensions.Configuration;

namespace ExportIfc.Settings.Loading;

/// <summary>
/// Загрузка <see cref="AppSettings"/> из ini-файла.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Проверяет доступность <see cref="ProjectFileNames.SettingsIni"/>.
/// 2. Строит <see cref="IConfiguration"/> поверх найденного ini-файла.
/// 3. Читает обязательные и дефолтные параметры и собирает <see cref="AppSettings"/>.
/// 4. Определяет итоговый путь к <see cref="ProjectDirectoryNames.AdminData"/>
///    для текущего режима запуска.
///
/// Контракты:
/// 1. Путь к ini-файлу приводится к полному пути до чтения настроек.
/// 2. Отсутствие ini-файла считается ошибкой загрузки, а не поводом молча продолжать работу.
/// 3. Разбор строк ini и применение значений по умолчанию выполняются здесь,
///    а не в <see cref="AppSettings"/>.
/// 4. В production-режиме путь <see cref="ProjectDirectoryNames.AdminData"/>
///    читается из параметра <see cref="SettingsIniKeys.DirAdminData"/>.
/// 5. В непроизводственном режиме путь <see cref="ProjectDirectoryNames.AdminData"/>
///    вычисляется относительно расположения ini-файла и не требует
///    обязательного значения <see cref="SettingsIniKeys.DirAdminData"/>
///    в ini-файле.
/// </remarks>
public static class AppSettingsLoader
{
    /// <summary>
    /// Загружает настройки из <see cref="ProjectFileNames.SettingsIni"/>.
    /// </summary>
    /// <param name="iniPath">Полный или относительный путь к ini-файлу.</param>
    /// <returns>Экземпляр настроек приложения.</returns>
    public static AppSettings Load(string iniPath)
    {
        var fullIniPath = FileSystemEx.NormalizeExistingFilePath(
            iniPath,
            ProjectFileNames.SettingsIni);
        var configuration = BuildConfiguration(fullIniPath);

        return ReadSettings(configuration, fullIniPath);
    }

    /// <summary>
    /// Строит <see cref="IConfiguration"/> для указанного ini-файла.
    /// </summary>
    /// <param name="fullIniPath">Полный путь к существующему ini-файлу.</param>
    /// <returns>Сконфигурированный объект <see cref="IConfiguration"/>.</returns>
    /// <remarks>
    /// Базовым каталогом конфигурации становится папка ini-файла,
    /// а в AddIniFile передаётся только имя файла.
    /// </remarks>
    private static IConfiguration BuildConfiguration(string fullIniPath)
    {
        var iniDirectory = Path.GetDirectoryName(fullIniPath);
        if (string.IsNullOrWhiteSpace(iniDirectory))
        {
            throw new InvalidOperationException(
                $"Не удалось определить каталог файла {ProjectFileNames.SettingsIni}: {fullIniPath}");
        }

        var iniFileName = Path.GetFileName(fullIniPath);

        return new ConfigurationBuilder()
            .SetBasePath(iniDirectory)
            .AddIniFile(iniFileName, optional: false, reloadOnChange: false)
            .Build();
    }

    /// <summary>
    /// Читает значения из <see cref="IConfiguration"/> и собирает объект настроек приложения.
    /// </summary>
    /// <param name="configuration">Источник уже загруженных ini-значений.</param>
    /// <param name="fullIniPath">Полный путь к исходному ini-файлу.</param>
    /// <returns>Подготовленный экземпляр <see cref="AppSettings"/>.</returns>
    /// <remarks>
    /// Обязательные параметры читаются через методы ReadRequired*,
    /// а необязательные — через методы ReadDefault* с проектными значениями по умолчанию.
    /// </remarks>
    private static AppSettings ReadSettings(IConfiguration configuration, string fullIniPath)
    {
        var runtime = ReadRuntimeSettings(configuration, fullIniPath);
        var mapping = ReadMappingSettings(configuration);
        var excelAndRevit = ReadExcelAndRevitSettings(configuration);

        return new AppSettings(
            dirExportConfig: runtime.DirExportConfig,
            dirAdminData: runtime.DirAdminData,
            mappingDirCommon: mapping.MappingDirCommon,
            mappingDirLayers: mapping.MappingDirLayers,
            isProdMode: runtime.IsProdMode,
            runRevit: runtime.RunRevit,
            enableUnmappedExport: runtime.EnableUnmappedExport,
            revitBatchTimeoutMinutes: runtime.RevitBatchTimeoutMinutes,
            configJsonName: mapping.ConfigJsonName,
            sheetPath: excelAndRevit.SheetPath,
            sheetIgnore: excelAndRevit.SheetIgnore,
            sheetHistory: excelAndRevit.SheetHistory,
            revitExportView3dName: excelAndRevit.RevitExportView3dName,
            revitVersions: excelAndRevit.RevitVersions);
    }

    /// <summary>
    /// Читает базовые runtime-настройки запуска.
    /// </summary>
    /// <param name="configuration">Источник ini-значений.</param>
    /// <param name="fullIniPath">Полный путь к исходному ini-файлу.</param>
    /// <returns>
    /// Набор значений, которые определяют каталоги и режим выполнения прогона.
    /// </returns>
    /// <remarks>
    /// Здесь читаются параметры, от которых зависит поведение оркестратора как процесса:
    /// базовый каталог конфигурации, режим production/non-production,
    /// путь к административным данным и флаги запуска Revit.
    /// </remarks>
    private static (
        string DirExportConfig,
        string DirAdminData,
        bool IsProdMode,
        bool RunRevit,
        bool EnableUnmappedExport,
        int RevitBatchTimeoutMinutes)
        ReadRuntimeSettings(IConfiguration configuration, string fullIniPath)
    {
        var dirExportConfig = AppSettingsValueReader.ReadRequired(
            configuration,
            SettingsIniKeys.DirExportConfig);

        var isProdMode = AppSettingsValueReader.ReadRequiredBool(
            configuration,
            SettingsIniKeys.IsProdMode);

        var dirAdminData = ResolveAdminDataDirectory(
            configuration,
            fullIniPath,
            isProdMode);

        var runRevit = AppSettingsValueReader.ReadRequiredBool(
            configuration,
            SettingsIniKeys.RunRevit);

        var enableUnmappedExport = AppSettingsValueReader.ReadRequiredBool(
            configuration,
            SettingsIniKeys.EnableUnmappedExport);

        var revitBatchTimeoutMinutes = AppSettingsValueReader.ReadDefaultInt(
            configuration,
            SettingsIniKeys.RevitBatchTimeoutMinutes,
            ProjectDefaults.RevitBatchTimeoutMinutes);

        return (
            DirExportConfig: dirExportConfig,
            DirAdminData: dirAdminData,
            IsProdMode: isProdMode,
            RunRevit: runRevit,
            EnableUnmappedExport: enableUnmappedExport,
            RevitBatchTimeoutMinutes: revitBatchTimeoutMinutes);
    }

    /// <summary>
    /// Читает параметры маппинга и имена артефактов IFC-настроек.
    /// </summary>
    /// <param name="configuration">Источник ini-значений.</param>
    /// <returns>Набор значений для поиска каталогов маппинга и JSON-конфига IFC.</returns>
    private static (
        string MappingDirCommon,
        string MappingDirLayers,
        string ConfigJsonName)
        ReadMappingSettings(IConfiguration configuration)
    {
        var mappingDirCommon = AppSettingsValueReader.ReadDefault(
            configuration,
            SettingsIniKeys.MappingDirCommon,
            ProjectDefaults.MappingDirCommon);

        var mappingDirLayers = AppSettingsValueReader.ReadDefault(
            configuration,
            SettingsIniKeys.MappingDirLayers,
            ProjectDefaults.MappingDirLayers);

        var configJsonName = AppSettingsValueReader.ReadRequired(
            configuration,
            SettingsIniKeys.ConfigJson);

        return (
            MappingDirCommon: mappingDirCommon,
            MappingDirLayers: mappingDirLayers,
            ConfigJsonName: configJsonName);
    }

    /// <summary>
    /// Читает имена Excel-листов и параметры, связанные с запуском Revit.
    /// </summary>
    /// <param name="configuration">Источник ini-значений.</param>
    /// <returns>Набор значений для чтения Excel-данных и построения batch-плана Revit.</returns>
    private static (
        string SheetPath,
        string SheetIgnore,
        string SheetHistory,
        string RevitExportView3dName,
        IReadOnlyList<int> RevitVersions)
        ReadExcelAndRevitSettings(IConfiguration configuration)
    {
        var sheetPath = AppSettingsValueReader.ReadDefault(
            configuration,
            SettingsIniKeys.SheetPath,
            ProjectDefaults.SheetPath);

        var sheetIgnore = AppSettingsValueReader.ReadDefault(
            configuration,
            SettingsIniKeys.SheetIgnore,
            ProjectDefaults.SheetIgnore);

        var sheetHistory = AppSettingsValueReader.ReadDefault(
            configuration,
            SettingsIniKeys.SheetHistory,
            ProjectDefaults.SheetHistory);

        var revitExportView3dName = AppSettingsValueReader.ReadDefault(
            configuration,
            SettingsIniKeys.RevitExportView3dName,
            ProjectDefaults.RevitExportView3dName);

        var revitVersions = AppSettingsValueReader.ReadRequiredIntList(
            configuration,
            SettingsIniKeys.RevitVersions);

        return (
            SheetPath: sheetPath,
            SheetIgnore: sheetIgnore,
            SheetHistory: sheetHistory,
            RevitExportView3dName: revitExportView3dName,
            RevitVersions: revitVersions);
    }

    /// <summary>
    /// Определяет итоговый путь к каталогу <see cref="ProjectDirectoryNames.AdminData"/>.
    /// </summary>
    /// <param name="configuration">Источник ini-значений.</param>
    /// <param name="fullIniPath">Полный путь к исходному ini-файлу.</param>
    /// <param name="isProdMode">Признак production-режима.</param>
    /// <returns>Итоговый путь к каталогу административных данных.</returns>
    /// <remarks>
    /// В production-режиме путь читается из ini-файла через
    /// <see cref="SettingsIniKeys.DirAdminData"/>.
    /// В непроизводственном режиме путь строится от результата
    /// <see cref="ResolveProjectRoot(string)"/> и указывает на каталог
    /// <see cref="ProjectDirectoryNames.AdminData"/>.
    /// </remarks>
    private static string ResolveAdminDataDirectory(
        IConfiguration configuration,
        string fullIniPath,
        bool isProdMode)
    {
        if (isProdMode)
        {
            return AppSettingsValueReader.ReadRequired(
                configuration,
                SettingsIniKeys.DirAdminData);
        }

        var projectRoot = ResolveProjectRoot(fullIniPath);
        return Path.Combine(projectRoot, ProjectDirectoryNames.AdminData);
    }

    /// <summary>
    /// Определяет корень проекта для локального режима запуска.
    /// </summary>
    /// <param name="fullIniPath">Полный путь к исходному ini-файлу.</param>
    /// <returns>Полный путь к корню проекта.</returns>
    /// <remarks>
    /// Если ini-файл лежит в каталоге <see cref="ProjectDirectoryNames.Settings"/>,
    /// корнем считается его родительская директория.
    /// Во всех остальных случаях корнем считается каталог самого ini-файла.
    /// </remarks>
    private static string ResolveProjectRoot(string fullIniPath)
    {
        var iniDirectory = Path.GetDirectoryName(fullIniPath);
        if (string.IsNullOrWhiteSpace(iniDirectory))
        {
            throw new InvalidOperationException(
                $"Не удалось определить каталог файла {ProjectFileNames.SettingsIni}: {fullIniPath}");
        }

        var fullIniDirectory = Path.GetFullPath(iniDirectory);
        var iniDirectoryInfo = new DirectoryInfo(fullIniDirectory);

        if (iniDirectoryInfo.Name.Equals(
                ProjectDirectoryNames.Settings,
                StringComparison.OrdinalIgnoreCase))
        {
            var parent = iniDirectoryInfo.Parent;
            if (parent is not null)
                return parent.FullName;
        }

        return fullIniDirectory;
    }
}