using ExportIfc.Settings.Schema;

namespace ExportIfc.Tests.TestInfrastructure;

/// <summary>
/// Построитель ini-текста для тестов загрузки настроек и startup-резолвинга.
/// </summary>
/// <remarks>
/// Helper собирает только те секции и ключи, которые нужны текущему тесту.
/// Он не должен становиться альтернативной реализацией production-конфига.
/// </remarks>
internal static class TestIniBuilder
{
    /// <summary>
    /// Строит минимальный ini-файл, достаточный для startup-резолвинга.
    /// </summary>
    /// <returns>Текст минимального ini-файла.</returns>
    public static string BuildMinimal()
    {
        return string.Join(
            Environment.NewLine,
            $"[{SettingsIniSections.Settings}]",
            $"{SettingsIniKeys.RunRevitName} = True");
    }

    /// <summary>
    /// Строит типовой ini-файл для загрузчика <see cref="ExportIfc.Settings.Loading.AppSettingsLoader"/>.
    /// </summary>
    /// <param name="exportConfig">Путь к каталогу export-config.</param>
    /// <param name="adminData">Путь к каталогу admin_data для production-режима.</param>
    /// <param name="isProdMode">Признак production-режима.</param>
    /// <returns>Текст ini-файла с типовым набором настроек.</returns>
    public static string BuildAppSettings(string exportConfig, string? adminData, bool isProdMode)
    {
        var lines = new List<string>
        {
            $"[{SettingsIniSections.Paths}]",
            $"{SettingsIniKeys.DirExportConfigName} = {exportConfig}"
        };

        if (!string.IsNullOrWhiteSpace(adminData))
            lines.Add($"{SettingsIniKeys.DirAdminDataName} = {adminData}");

        lines.Add(string.Empty);
        lines.Add($"[{SettingsIniSections.Files}]");
        lines.Add($"{SettingsIniKeys.ConfigJsonName} = Export_Settings");

        lines.Add(string.Empty);
        lines.Add($"[{SettingsIniSections.Settings}]");
        lines.Add($"{SettingsIniKeys.IsProdModeName} = {(isProdMode ? "True" : "False")}");
        lines.Add($"{SettingsIniKeys.RunRevitName} = True");
        lines.Add($"{SettingsIniKeys.EnableUnmappedExportName} = False");
        lines.Add($"{SettingsIniKeys.RevitBatchTimeoutMinutesName} = 0");

        lines.Add(string.Empty);
        lines.Add($"[{SettingsIniSections.Revit}]");
        lines.Add($"{SettingsIniKeys.RevitVersionsName} = 2022,2023");
        lines.Add($"{SettingsIniKeys.RevitExportView3dNameName} = Navisworks");

        lines.Add(string.Empty);
        lines.Add($"[{SettingsIniSections.Excel}]");
        lines.Add($"{SettingsIniKeys.SheetPathName} = Path");
        lines.Add($"{SettingsIniKeys.SheetIgnoreName} = IgnoreList");
        lines.Add($"{SettingsIniKeys.SheetHistoryName} = History");

        lines.Add(string.Empty);
        lines.Add($"[{SettingsIniSections.Mapping}]");
        lines.Add($"{SettingsIniKeys.MappingDirCommonName} = 00_Common");
        lines.Add($"{SettingsIniKeys.MappingDirLayersName} = 01_Layers");

        return string.Join(Environment.NewLine, lines);
    }
}
