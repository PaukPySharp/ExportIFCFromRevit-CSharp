namespace ExportIfc.RevitAddin.Config.Ifc;

/// <summary>
/// Значения по умолчанию и служебные константы для подготовки IFC-конфигурации.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует tweakable-значения IFC-конфига.
/// 2. Убирает магические числа и служебные шаблоны из платформенных фабрик IFC-опций.
///
/// Контракты:
/// 1. Значения используются только при подготовке IFC-конфигурации add-in.
/// 2. Класс не содержит пользовательских текстов и логовых фраз.
/// </remarks>
internal static class IfcConfigDefaults
{
    /// <summary>
    /// Regex-шаблон для извлечения числа миллисекунд Unix Epoch из строки.
    /// </summary>
    public const string UnixEpochMillisecondsPattern = @"-?\d+";

    /// <summary>
    /// Начальная точка Unix Epoch.
    /// </summary>
    public static readonly DateTime UnixEpochStart = new(1970, 1, 1);
}