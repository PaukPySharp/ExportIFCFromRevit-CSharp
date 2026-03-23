using ExportIfc.Models;

namespace ExportIfc.Tests.TestInfrastructure;

/// <summary>
/// Фабрика тестовых моделей Revit.
/// </summary>
/// <remarks>
/// Позволяет быстро собрать <see cref="RevitModel"/> с предсказуемыми путями
/// к конфигурационным файлам. При необходимости отдельные пути можно переопределить,
/// чтобы тест явно моделировал нужный transport- или orchestration-контракт.
/// </remarks>
internal static class TestModelFactory
{
    /// <summary>
    /// Создаёт тестовую модель Revit.
    /// </summary>
    /// <param name="rvtPath">Полный путь к RVT-файлу.</param>
    /// <param name="lastModifiedMinute">Нормализованное время модификации модели.</param>
    /// <param name="outputDirMapping">Каталог выгрузки IFC с маппингом.</param>
    /// <param name="outputDirNoMap">Каталог выгрузки IFC без маппинга.</param>
    /// <param name="mappingJson">Явный путь к JSON-конфигурации маппинга.</param>
    /// <param name="ifcClassMappingFile">Явный путь к файлу сопоставления категорий и классов IFC.</param>
    /// <param name="noMapJson">
    /// Явный путь к JSON-конфигурации no-map маршрута.
    /// Может быть задан даже при <paramref name="outputDirNoMap"/> = <see langword="null"/>,
    /// если тест моделирует уже построенную export-projection.
    /// </param>
    /// <returns>Подготовленный экземпляр <see cref="RevitModel"/>.</returns>
    public static RevitModel Create(
        string rvtPath,
        DateTime lastModifiedMinute,
        string? outputDirMapping = null,
        string? outputDirNoMap = null,
        string? mappingJson = null,
        string? ifcClassMappingFile = null,
        string? noMapJson = null)
    {
        var baseDir = Path.GetDirectoryName(rvtPath) ?? Path.GetTempPath();

        return new RevitModel
        {
            RvtPath = rvtPath,
            LastModifiedMinute = lastModifiedMinute,
            OutputDirMapping = outputDirMapping,
            MappingJson = mappingJson ?? Path.Combine(baseDir, "mapping.json"),
            IfcClassMappingFile = ifcClassMappingFile ?? Path.Combine(baseDir, "family-mapping.txt"),
            OutputDirNoMap = outputDirNoMap,
            NoMapJson = noMapJson ?? (outputDirNoMap is null
                ? null
                : Path.Combine(baseDir, "nomap.json"))
        };
    }
}
