using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using ExportIfc.Config;
using ExportIfc.RevitAddin.Batch.Context;
using ExportIfc.RevitAddin.Batch.Export.Execution;
using ExportIfc.RevitAddin.Logging;
using ExportIfc.RevitAddin.Revit;
using ExportIfc.Transfer;

namespace ExportIfc.RevitAddin.Batch.Export;

/// <summary>
/// Координирует обработку одной модели внутри batch-запуска.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Проверить доступность файла модели.
/// 2. Открыть документ Revit.
/// 3. Найти целевой 3D-вид.
/// 4. Подготовить контекст выполнения модели.
/// 5. Выполнить одну или две независимые open-session попытки экспорта через исполнитель попытки.
/// 6. Безопасно закрыть документ после каждой open-session попытки.
///
/// Контракты:
/// 1. Метод <see cref="ExportOne"/> обрабатывает только одну модель за вызов.
/// 2. Класс управляет жизненным циклом документа, но не содержит низкоуровневую логику маршрутов экспорта.
/// 3. Если первая open-session попытка завершилась формально успешно,
///    но дала только подозрительно маленький IFC, выполняется ещё одна попытка
///    через полное переоткрытие модели.
/// 4. Повторный экспорт выполняется через полное закрытие и повторное открытие модели,
///    а не повтором в том же экземпляре <see cref="Document"/>.
/// </remarks>
internal sealed class ModelExportExecutor : IModelExportExecutor
{
    private readonly ModelExportAttemptExecutor _attemptExecutor;

    /// <summary>
    /// Создаёт исполнитель экспорта одной модели.
    /// </summary>
    /// <param name="ifcExportOptionsFactory">
    /// Фабрика построения готовых IFC-опций для текущей платформенной ветки add-in.
    /// </param>
    internal ModelExportExecutor(IIfcExportOptionsFactory ifcExportOptionsFactory)
    {
        _attemptExecutor = new ModelExportAttemptExecutor(ifcExportOptionsFactory);
    }

    /// <summary>
    /// Выполняет экспорт одной модели.
    /// </summary>
    /// <param name="uiApp">Текущий UI-контекст Revit.</param>
    /// <param name="item">Описание модели и путей экспорта.</param>
    /// <param name="context">Контекст batch-запуска add-in.</param>
    /// <returns>
    /// <see langword="true"/>, если модель успешно прошла все требуемые фазы открытия,
    /// экспорта и закрытия;
    /// иначе <see langword="false"/>.
    /// </returns>
    public bool ExportOne(
        UIApplication uiApp,
        TransferItem item,
        BatchRunContext context)
    {
        if (!File.Exists(item.ModelPath))
        {
            WriteOpeningError(context, item.ModelPath, "файл модели не найден на диске");
            return false;
        }

        return ExecuteWithRetry(uiApp, item, context);
    }

    /// <summary>
    /// Выполняет экспорт модели и при необходимости один раз повторяет попытку
    /// через полное переоткрытие документа.
    /// </summary>
    /// <param name="uiApp">Текущий UI-контекст Revit.</param>
    /// <param name="item">Описание модели и путей экспорта.</param>
    /// <param name="context">Контекст batch-запуска add-in.</param>
    /// <returns>
    /// <see langword="true"/>, если итоговый экспорт модели завершился успешно;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Retry выполняется через отдельную open-session попытку.
    /// Сценарий используется для случаев, когда первая попытка формально завершилась
    /// успешно, но дала только подозрительно маленький IFC. Повторный экспорт
    /// выполняется после полного переоткрытия модели, а не внутри того же
    /// экземпляра <see cref="Document"/>, чтобы сбросить состояние export-session.
    /// </remarks>
    private bool ExecuteWithRetry(
        UIApplication uiApp,
        TransferItem item,
        BatchRunContext context)
    {
        var firstSessionSucceeded = ExecuteSingleOpenSession(
            uiApp,
            item,
            context,
            out var firstAttempt);

        if (!firstSessionSucceeded)
            return false;

        if (!firstAttempt.HasOnlySuspiciousSmallIfc)
            return true;

        AddinLogs.WriteStartup(
            context.DirAdminData,
            $"Подозрительно маленький IFC на первой попытке. Выполняется повторный экспорт с полным переоткрытием модели. " +
            $"Модель='{GetModelLogName(item.ModelPath)}'");

        var retrySessionSucceeded = ExecuteSingleOpenSession(
            uiApp,
            item,
            context,
            out var retryAttempt);

        if (!retrySessionSucceeded)
            return false;

        if (retryAttempt.HasOnlySuspiciousSmallIfc)
        {
            AddinLogs.WriteDaily(
                context.DirAdminData,
                LogFiles.ExportErrors,
                $"{item.ModelPath} - после повторного открытия и повторной попытки IFC остался подозрительно маленьким. Экспорт помечен как ошибочный.");

            return false;
        }

        AddinLogs.WriteStartup(
            context.DirAdminData,
            $"Повторный экспорт после переоткрытия модели завершился успешно. Модель='{GetModelLogName(item.ModelPath)}'");

        return true;
    }

    /// <summary>
    /// Выполняет одну полную open-session попытку экспорта модели.
    /// </summary>
    /// <param name="uiApp">Текущий UI-контекст Revit.</param>
    /// <param name="item">Описание модели и путей экспорта.</param>
    /// <param name="context">Контекст batch-запуска add-in.</param>
    /// <param name="attemptResult">Итог попытки экспорта внутри открытого документа.</param>
    /// <returns>
    /// <see langword="true"/>, если модель открыта, подготовлена, экспортирована
    /// и затем штатно закрыта;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Метод выполняет одну полную open-session фазу:
    /// открыть документ, найти export-view, выполнить одну попытку экспорта,
    /// затем закрыть документ.
    ///
    /// Такое ограничение позволяет повторять экспорт на чистом состоянии документа
    /// и держать retry-логику на уровне отдельных open-session попыток,
    /// а не внутри одного открытого экземпляра модели.
    /// </remarks>
    private bool ExecuteSingleOpenSession(
        UIApplication uiApp,
        TransferItem item,
        BatchRunContext context,
        out ExportAttemptResult attemptResult)
    {
        Document? document = null;
        View3D? exportView = null;
        var sessionSucceeded = false;

        attemptResult = ExportAttemptResult.FailedWithoutExports();

        try
        {
            if (TryOpenDocument(uiApp, item, context, out document) &&
                document is not null &&
                TryFindExportView(document, item.ModelPath, context, out exportView) &&
                exportView is not null)
            {
                var executionContext = new ModelExportExecutionContext(
                    document,
                    exportView,
                    item,
                    context);

                attemptResult = _attemptExecutor.Execute(executionContext);
                sessionSucceeded = attemptResult.Succeeded;
            }
        }
        finally
        {
            if (!CloseDocument(document, item.ModelPath, context.DirAdminData))
                sessionSucceeded = false;
        }

        return sessionSucceeded;
    }

    /// <summary>
    /// Пытается открыть модель Revit для пакетной обработки.
    /// </summary>
    /// <param name="uiApp">Текущий UI-контекст Revit.</param>
    /// <param name="item">Описание модели текущего пакета.</param>
    /// <param name="context">Контекст batch-запуска add-in.</param>
    /// <param name="document">Открытый документ Revit, если модель удалось открыть.</param>
    /// <returns>
    /// <see langword="true"/>, если документ успешно открыт;
    /// иначе <see langword="false"/>.
    /// </returns>
    private static bool TryOpenDocument(
        UIApplication uiApp,
        TransferItem item,
        BatchRunContext context,
        out Document? document)
    {
        document = null;

        try
        {
            document = RevitDocuments.OpenForBatchProcessing(
                uiApp.Application,
                item.ModelPath);
        }
        catch (Exception ex)
        {
            WriteOpeningError(context, item.ModelPath, $"модель не открылась в Revit ({ex})");
            return false;
        }

        if (document is null)
        {
            WriteOpeningError(context, item.ModelPath, "модель не открылась в Revit (OpenDocumentFile вернул null)");
            return false;
        }

        var isDetached = document.IsDetached;
        var openMode = isDetached ? "Detached" : "Direct";
        var openedTitle = isDetached ? document.Title : GetModelLogName(item.ModelPath);

        AddinLogs.WriteStartup(
            context.DirAdminData,
            $"Модель открыта. Source='{item.ModelPath}' | OpenedTitle='{openedTitle}' | OpenMode={openMode}");

        return true;
    }

    /// <summary>
    /// Пытается найти целевой 3D-вид для IFC-экспорта.
    /// </summary>
    /// <param name="document">Открытый документ Revit.</param>
    /// <param name="modelPath">Путь до исходной RVT-модели</param>
    /// <param name="context">Контекст batch-запуска add-in.</param>
    /// <param name="exportView">Найденный 3D-вид для экспорта.</param>
    /// <returns>
    /// <see langword="true"/>, если требуемый 3D-вид найден;
    /// иначе <see langword="false"/>.
    /// </returns>
    private static bool TryFindExportView(
        Document document,
        string modelPath,
        BatchRunContext context,
        out View3D? exportView)
    {
        exportView = null;

        try
        {
            exportView = RevitViews.FindView3DByName(document, context.ExportViewName);
        }
        catch (Exception ex)
        {
            WriteOpeningError(context, modelPath, $"ошибка при поиске 3D-вида ({ex})");
            return false;
        }

        if (exportView is null)
        {
            WriteMissingViewError(context, modelPath);
            return false;
        }

        var viewId = RevitElementIds.ToLogValue(exportView.Id);
        var perspectiveSuffix = exportView.IsPerspective ? " | IsPerspective=True" : string.Empty;

        AddinLogs.WriteStartup(
            context.DirAdminData,
            $"Выбран вид экспорта. Name='{exportView.Name}' | Id={viewId}{perspectiveSuffix}");

        return true;
    }

    /// <summary>
    /// Пытается штатно закрыть документ после обработки модели.
    /// </summary>
    /// <param name="document">Документ Revit, подлежащий закрытию.</param>
    /// <param name="modelPath">Исходный путь модели для логирования.</param>
    /// <param name="dirAdminData">Рабочий каталог admin-data текущего запуска.</param>
    /// <returns>
    /// <see langword="true"/>, если документ закрыт без ошибок;
    /// иначе <see langword="false"/>.
    /// </returns>
    private static bool CloseDocument(Document? document, string modelPath, string dirAdminData)
    {
        var closeError = RevitDocuments.CloseSafely(document);

        if (string.IsNullOrWhiteSpace(closeError))
            return true;

        AddinLogs.WriteDaily(
            dirAdminData,
            LogFiles.ExportErrors,
            $"{modelPath} - документ закрылся с ошибкой после экспорта ({closeError})");

        return false;
    }

    /// <summary>
    /// Пишет ошибку фазы открытия или подготовки модели в startup- и daily-логи.
    /// </summary>
    /// <param name="context">Контекст batch-запуска add-in.</param>
    /// <param name="modelPath">Путь модели, для которой произошла ошибка.</param>
    /// <param name="message">Текст диагностического сообщения.</param>
    private static void WriteOpeningError(BatchRunContext context, string modelPath, string message)
    {
        AddinLogs.WriteStartup(
            context.DirAdminData,
            $"Ошибка открытия/подготовки модели. Model='{modelPath}' | Message='{message}'");

        AddinLogs.WriteDaily(
            context.DirAdminData,
            LogFiles.OpeningErrors,
            $"{modelPath} - {message}");
    }

    /// <summary>
    /// Пишет ошибку отсутствия экспортного вида в startup- и daily-логи.
    /// </summary>
    /// <param name="context">Контекст batch-запуска add-in.</param>
    /// <param name="modelPath">Путь модели, в которой не найден экспортный вид.</param>
    private static void WriteMissingViewError(BatchRunContext context, string modelPath)
    {
        AddinLogs.WriteStartup(
            context.DirAdminData,
            $"Не найден 3D-вид экспорта. Model='{modelPath}' | ViewName='{context.ExportViewName}'");

        AddinLogs.WriteDaily(
            context.DirAdminData,
            LogFiles.MissingView(context.ExportViewName),
            $"{modelPath} - в модели отсутствует 3D-вид для экспорта ({context.ExportViewName})");
    }

    /// <summary>
    /// Возвращает короткое имя модели для компактного технического лога.
    /// </summary>
    /// <param name="modelPath">Исходный путь модели.</param>
    /// <returns>
    /// Имя файла модели, если его удалось выделить из пути;
    /// иначе исходное значение <paramref name="modelPath"/>.
    /// </returns>
    private static string GetModelLogName(string modelPath)
    {
        var fileName = Path.GetFileName(modelPath);
        return string.IsNullOrWhiteSpace(fileName) ? modelPath : fileName;
    }
}
