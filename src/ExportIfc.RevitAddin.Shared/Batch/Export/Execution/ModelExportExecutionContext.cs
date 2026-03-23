using Autodesk.Revit.DB;

using ExportIfc.RevitAddin.Batch.Context;
using ExportIfc.Transfer;

namespace ExportIfc.RevitAddin.Batch.Export.Execution;

/// <summary>
/// Контекст обработки одной модели в уже открытом документе Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Собрать в одном типе данные, которые естественно живут вместе на фазе экспорта модели.
/// 2. Убрать повторяющуюся передачу связанных аргументов по множеству методов.
///
/// Контракты:
/// 1. Экземпляр создаётся только для уже открытого документа и найденного export-view.
/// 2. Класс не открывает документ, не ищет вид и не выполняет экспорт сам по себе.
/// </remarks>
internal sealed class ModelExportExecutionContext
{
    /// <summary>
    /// Создаёт контекст обработки одной модели.
    /// </summary>
    /// <param name="document">Открытый документ Revit.</param>
    /// <param name="exportView">Выбранный 3D-вид для экспорта.</param>
    /// <param name="item">Описание модели и путей экспорта.</param>
    /// <param name="batchContext">Контекст batch-запуска add-in.</param>
    public ModelExportExecutionContext(
        Document document,
        View3D exportView,
        TransferItem item,
        BatchRunContext batchContext)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        ExportView = exportView ?? throw new ArgumentNullException(nameof(exportView));
        Item = item ?? throw new ArgumentNullException(nameof(item));
        BatchContext = batchContext ?? throw new ArgumentNullException(nameof(batchContext));

        DirAdminData = batchContext.DirAdminData;
        IfcName = Path.GetFileNameWithoutExtension(item.ModelPath);
    }

    /// <summary>
    /// Получает открытый документ Revit.
    /// </summary>
    public Document Document { get; }

    /// <summary>
    /// Получает выбранный 3D-вид для экспорта.
    /// </summary>
    public View3D ExportView { get; }

    /// <summary>
    /// Получает описание модели и путей экспорта.
    /// </summary>
    public TransferItem Item { get; }

    /// <summary>
    /// Получает контекст batch-запуска add-in.
    /// </summary>
    public BatchRunContext BatchContext { get; }

    /// <summary>
    /// Получает рабочий каталог admin-data текущего запуска.
    /// </summary>
    public string DirAdminData { get; }

    /// <summary>
    /// Получает базовое имя IFC-файла без расширения.
    /// </summary>
    public string IfcName { get; }
}