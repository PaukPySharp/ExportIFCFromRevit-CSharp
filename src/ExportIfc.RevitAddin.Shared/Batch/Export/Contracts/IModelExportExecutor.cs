using Autodesk.Revit.UI;

using ExportIfc.RevitAddin.Batch.Context;
using ExportIfc.Transfer;

namespace ExportIfc.RevitAddin.Batch.Export;

/// <summary>
/// Контракт экспорта одной модели внутри Revit add-in.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Изолировать выполнение экспорта одной модели от координатора batch-пакета.
/// 2. Дать batch-исполнителю единый способ получить итог успешности обработки модели.
///
/// Контракты:
/// 1. Реализация сама отвечает за полный цикл обработки одной модели в текущей сессии Revit.
/// 2. Возвращаемое значение отражает итог успешности обработки модели целиком.
/// </remarks>
internal interface IModelExportExecutor
{
    /// <summary>
    /// Выполняет экспорт одной модели.
    /// </summary>
    /// <param name="uiApp">Текущий UI-контекст Revit.</param>
    /// <param name="item">Описание модели и путей экспорта в рамках текущего пакета.</param>
    /// <param name="context">Контекст batch-запуска add-in.</param>
    /// <returns>
    /// <see langword="true"/>, если модель обработана без ошибок;
    /// иначе <see langword="false"/>.
    /// </returns>
    bool ExportOne(UIApplication uiApp, TransferItem item, BatchRunContext context);
}