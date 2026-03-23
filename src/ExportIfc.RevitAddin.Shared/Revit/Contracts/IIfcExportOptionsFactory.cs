using Autodesk.Revit.DB;

namespace ExportIfc.RevitAddin.Revit;

/// <summary>
/// Контракт построения <see cref="IFCExportOptions"/> для экспорта модели.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Скрыть платформенно-зависимую подготовку IFC-опций за единым runtime-контрактом.
/// 2. Дать batch-исполнителю общий способ получить готовые опции экспорта.
///
/// Контракты:
/// 1. Реализация обязана вернуть полностью подготовленный экземпляр <see cref="IFCExportOptions"/>.
/// 2. Реализация сама отвечает за чтение конфигурации IFC и нормализацию нужных значений.
/// </remarks>
internal interface IIfcExportOptionsFactory
{
    /// <summary>
    /// Создаёт и настраивает <see cref="IFCExportOptions"/> для заданного 3D-вида.
    /// </summary>
    /// <param name="document">Открытый документ Revit, из которого выполняется экспорт.</param>
    /// <param name="ifcClassMappingFile">Путь к txt-файлу сопоставления категорий Revit и классов IFC.</param>
    /// <param name="configJsonPath">Путь к JSON-файлу настроек IFC.</param>
    /// <param name="viewId">Идентификатор 3D-вида для экспорта.</param>
    /// <returns>Готовые опции экспорта IFC.</returns>
    IFCExportOptions Create(
        Document document,
        string ifcClassMappingFile,
        string configJsonPath,
        ElementId viewId);
}