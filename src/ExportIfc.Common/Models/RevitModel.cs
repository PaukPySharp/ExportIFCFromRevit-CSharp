using ExportIfc.Config;

namespace ExportIfc.Models;

/// <summary>
/// Описание одной модели Revit и связанных с ней параметров экспорта IFC.
/// </summary>
/// <remarks>
/// Назначение:
/// Хранит путь к модели, время модификации и параметры двух направлений экспорта:
/// с маппингом и без маппинга.
///
/// Контракты:
/// 1. Экземпляр модели описывает уже нормализованные и проверенные входные данные.
/// 2. Направление экспорта с маппингом считается обязательным:
///    путь к JSON-конфигурации и файл сопоставления категорий Revit и классов IFC должны быть заданы.
/// 3. Направление экспорта без маппинга считается опциональным:
///    связанные свойства могут быть пустыми.
/// 4. Для batch-плана строится отдельная проекция модели,
///    не изменяющая исходный экземпляр.
/// </remarks>
public sealed class RevitModel
{
    /// <summary>
    /// Абсолютный путь к RVT-файлу.
    /// </summary>
    public required string RvtPath { get; init; }

    /// <summary>
    /// Время модификации RVT, нормализованное до минут.
    /// </summary>
    public DateTime LastModifiedMinute { get; init; }

    /// <summary>
    /// Каталог выгрузки IFC с маппингом.
    /// </summary>
    public string? OutputDirMapping { get; init; }

    /// <summary>
    /// Путь к JSON-конфигурации маппинга.
    /// </summary>
    public required string MappingJson { get; init; }

    /// <summary>
    /// Путь к файлу сопоставления категорий Revit и классов IFC.
    /// </summary>
    public required string IfcClassMappingFile { get; init; }

    /// <summary>
    /// Каталог выгрузки IFC без маппинга.
    /// </summary>
    public string? OutputDirNoMap { get; init; }

    /// <summary>
    /// Путь к JSON-конфигурации для выгрузки без маппинга.
    /// </summary>
    public string? NoMapJson { get; init; }

    /// <summary>
    /// Строит проекцию модели для экспорта с учётом уже актуальных направлений.
    /// </summary>
    /// <param name="ifcMappingOk">
    /// Признак того, что IFC с маппингом уже актуален и не требует повторной выгрузки.
    /// </param>
    /// <param name="ifcNoMapOk">
    /// Признак того, что IFC без маппинга уже актуален и не требует повторной выгрузки.
    /// </param>
    /// <returns>Новая проекция модели, пригодная для постановки в batch-план.</returns>
    /// <remarks>
    /// Метод не изменяет исходный экземпляр.
    /// Для уже актуального направления каталог выгрузки обнуляется,
    /// чтобы downstream-логика могла явно пропустить этот маршрут.
    /// Остальные данные модели сохраняются без изменений.
    /// </remarks>
    public RevitModel CreateExportProjection(
        bool ifcMappingOk,
        bool ifcNoMapOk)
    {
        return new RevitModel
        {
            RvtPath = RvtPath,
            LastModifiedMinute = LastModifiedMinute,
            OutputDirMapping = ifcMappingOk ? null : OutputDirMapping,
            MappingJson = MappingJson,
            IfcClassMappingFile = IfcClassMappingFile,
            OutputDirNoMap = ifcNoMapOk ? null : OutputDirNoMap,
            NoMapJson = NoMapJson
        };
    }

    /// <summary>
    /// Возвращает ожидаемый путь к IFC-файлу с маппингом.
    /// </summary>
    /// <returns>
    /// Полный путь к ожидаемому IFC-файлу либо <see langword="null"/>,
    /// если направление экспорта с маппингом не активно.
    /// </returns>
    public string? ExpectedIfcPathMapping()
    {
        return BuildExpectedIfcPath(OutputDirMapping);
    }

    /// <summary>
    /// Возвращает ожидаемый путь к IFC-файлу без маппинга.
    /// </summary>
    /// <returns>
    /// Полный путь к ожидаемому IFC-файлу либо <see langword="null"/>,
    /// если направление экспорта без маппинга не активно.
    /// </returns>
    public string? ExpectedIfcPathNoMap()
    {
        return BuildExpectedIfcPath(OutputDirNoMap);
    }

    /// <summary>
    /// Строит ожидаемый путь к IFC-файлу для указанного каталога выгрузки.
    /// </summary>
    /// <param name="outputDirectory">Каталог выгрузки IFC.</param>
    /// <returns>
    /// Полный путь к ожидаемому IFC-файлу либо <see langword="null"/>,
    /// если каталог выгрузки не задан.
    /// </returns>
    private string? BuildExpectedIfcPath(string? outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            return null;

        return Path.Combine(
            outputDirectory,
            Path.GetFileNameWithoutExtension(RvtPath) + ProjectFileExtensions.Ifc);
    }
}
