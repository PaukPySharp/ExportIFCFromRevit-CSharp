namespace ExportIfc.Manage;

/// <summary>
/// Нормализованная конфигурация одной строки листа Path.
/// </summary>
/// <remarks>
/// Назначение:
/// Хранит итоговые проверенные значения одной строки листа Path,
/// которые уже готовы для поиска моделей и последующей оркестрации выгрузки.
///
/// Контракты:
/// 1. Обязательные свойства должны быть заполнены до передачи экземпляра
///    в следующий этап обработки.
/// 2. Значения путей в свойствах должны быть уже нормализованы и проверены
///    вызывающим кодом.
/// 3. Свойства для выгрузки без маппинга могут оставаться пустыми,
///    если такой режим не используется для строки.
/// </remarks>
internal sealed class ManagePathRowData
{
    /// <summary>
    /// Ключ строки для защиты от дубликатов.
    /// </summary>
    public required string RowKey { get; init; }

    /// <summary>
    /// Каталог RVT-моделей.
    /// </summary>
    public required string RvtDir { get; init; }

    /// <summary>
    /// Каталог выгрузки с маппингом.
    /// </summary>
    public required string OutputDirMapping { get; init; }

    /// <summary>
    /// Путь к JSON-конфигурации с маппингом.
    /// </summary>
    public required string MappingJson { get; init; }

    /// <summary>
    /// Путь к файлу сопоставления категорий Revit и классов IFC.
    /// </summary>
    public required string IfcClassMappingFile { get; init; }

    /// <summary>
    /// Каталог выгрузки без маппинга.
    /// </summary>
    public string? OutputDirNoMap { get; init; }

    /// <summary>
    /// Путь к JSON-конфигурации без маппинга.
    /// </summary>
    public string? NoMapJson { get; init; }
}
