namespace ExportIfc.RevitAddin.Batch.Export.Routes;

/// <summary>
/// Описание одного маршрута IFC-экспорта.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Собрать в одном типе параметры одного маршрута экспорта.
/// 2. Убрать длинные списки связанных аргументов из методов исполнения.
///
/// Контракты:
/// 1. Экземпляр описывает ровно один маршрут экспорта.
/// 2. Класс не содержит прикладной логики и не выполняет нормализацию путей.
/// </remarks>
internal sealed class ExportRouteRequest
{
    /// <summary>
    /// Создаёт описание маршрута IFC-экспорта.
    /// </summary>
    /// <param name="outputDirectory">Каталог назначения для IFC-файла.</param>
    /// <param name="ifcName">Базовое имя IFC-файла без расширения.</param>
    /// <param name="ifcClassMappingFile">Путь к файлу сопоставления категорий Revit и классов IFC.</param>
    /// <param name="configJsonPath">Путь к JSON-конфигурации IFC.</param>
    /// <param name="exportMode">Короткое имя маршрута для логов.</param>
    public ExportRouteRequest(
        string outputDirectory,
        string ifcName,
        string ifcClassMappingFile,
        string configJsonPath,
        string exportMode)
    {
        OutputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
        IfcName = ifcName ?? throw new ArgumentNullException(nameof(ifcName));
        IfcClassMappingFile = ifcClassMappingFile ?? throw new ArgumentNullException(nameof(ifcClassMappingFile));
        ConfigJsonPath = configJsonPath ?? throw new ArgumentNullException(nameof(configJsonPath));
        ExportMode = exportMode ?? throw new ArgumentNullException(nameof(exportMode));
    }

    /// <summary>
    /// Получает каталог назначения для IFC-файла.
    /// </summary>
    public string OutputDirectory { get; }

    /// <summary>
    /// Получает базовое имя IFC-файла без расширения.
    /// </summary>
    public string IfcName { get; }

    /// <summary>
    /// Получает путь к файлу сопоставления категорий Revit и классов IFC.
    /// </summary>
    public string IfcClassMappingFile { get; }

    /// <summary>
    /// Получает путь к JSON-конфигурации IFC.
    /// </summary>
    public string ConfigJsonPath { get; }

    /// <summary>
    /// Получает короткое имя маршрута для логов.
    /// </summary>
    public string ExportMode { get; }
}