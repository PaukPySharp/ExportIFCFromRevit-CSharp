using ExportIfc.Config;
using ExportIfc.Export.Planning;
using ExportIfc.Export.Runtime;
using ExportIfc.History;
using ExportIfc.IO;
using ExportIfc.Logging;

namespace ExportIfc.Export.Diagnostics;

/// <summary>
/// Сервис записи диагностических артефактов оркестратора.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Записывает вспомогательные txt-логи текущего прогона.
/// 2. Централизует best-effort работу с диагностическими файлами и Excel-историей.
/// 3. Не даёт побочным ошибкам диагностики ломать основной orchestration-сценарий.
///
/// Контракты:
/// 1. Ошибки записи диагностических txt-логов логируются, но не выбрасываются наружу.
/// 2. Логи, для которых разделитель добавляется на финализации, пишутся с завершающим разделителем.
/// 3. Сохранение истории возвращает явный признак успеха, а не бросает исключение в вызывающий код.
/// </remarks>
internal sealed class ExportDiagnosticsWriter
{
    private readonly IHistoryStore _historyStore;

    /// <summary>
    /// Создаёт сервис записи диагностических артефактов оркестратора.
    /// </summary>
    /// <param name="historyStore">
    /// Сервис чтения и записи <see cref="ProjectFileNames.HistoryWorkbook"/>.
    /// </param>
    internal ExportDiagnosticsWriter(IHistoryStore historyStore)
    {
        ArgumentNullException.ThrowIfNull(historyStore);
        _historyStore = historyStore;
    }

    /// <summary>
    /// Пишет лог по проблемам чтения времени модификации файлов.
    /// </summary>
    /// <param name="dirLogs">Каталог текстовых логов.</param>
    /// <param name="mTimeIssues">Список путей с проблемным определением mtime.</param>
    /// <param name="exportLog">Логгер текущего запуска.</param>
    /// <remarks>
    /// Лог пишется без завершающего разделителя: единый разделитель добавляется
    /// на этапе финализации, если файл действительно был затронут в этом прогоне.
    /// </remarks>
    public void WriteMTimeIssues(
        string dirLogs,
        IReadOnlyCollection<string> mTimeIssues,
        ConsoleLogger exportLog)
    {
        ArgumentNullException.ThrowIfNull(mTimeIssues);
        ArgumentNullException.ThrowIfNull(exportLog);

        if (mTimeIssues.Count == 0)
            return;

        var uniq = mTimeIssues
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!TryWriteLinesWithoutSeparator(dirLogs, LogFiles.MTimeIssues, uniq, exportLog))
            return;

        exportLog.Warn(
            "Не удалось определить дату изменения у {0} моделей. Подробности записаны в txt-лог.",
            uniq.Length);
    }

    /// <summary>
    /// Пишет диагностические логи по определению версии Revit.
    /// </summary>
    /// <param name="context">Контекст текущего запуска.</param>
    /// <param name="batchPlan">Построенный batch-план с диагностикой версий.</param>
    /// <param name="exportLog">Логгер текущего запуска.</param>
    /// <remarks>
    /// Логи version-not-found и version-too-new пишутся без завершающего разделителя.
    /// Итоговый разделитель добавляется на этапе финализации.
    /// </remarks>
    public void WriteVersionDiagnostics(
        ExportRunContext context,
        RevitBatchPlan batchPlan,
        ConsoleLogger exportLog)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(batchPlan);
        ArgumentNullException.ThrowIfNull(exportLog);

        if (batchPlan.VersionNotFound.Count > 0)
        {
            TryWriteLinesWithoutSeparator(
                context.Paths.DirLogs,
                LogFiles.VersionNotFound,
                batchPlan.VersionNotFound,
                exportLog);
        }

        if (batchPlan.VersionTooNew.Count > 0)
        {
            TryWriteLinesWithoutSeparator(
                context.Paths.DirLogs,
                LogFiles.VersionTooNew,
                batchPlan.VersionTooNew,
                exportLog);
        }
    }

    /// <summary>
    /// Добавляет разделители в затронутые txt-логи текущего запуска.
    /// </summary>
    /// <param name="context">Контекст текущего запуска.</param>
    /// <param name="exportLog">Логгер текущего запуска.</param>
    /// <remarks>
    /// Ошибка дозаписи разделителя рассматривается как диагностическая проблема
    /// и не должна срывать завершение прогона.
    /// </remarks>
    public void AppendTouchedSeparators(ExportRunContext context, ConsoleLogger exportLog)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(exportLog);

        var dirLogs = context.Paths.DirLogs;
        var startedAtUnix = context.StartedAtUnix;

        TryAppendSeparator(
            dirLogs,
            LogFiles.MissingView(context.Settings.RevitExportView3dName),
            startedAtUnix,
            exportLog);
        TryAppendSeparator(dirLogs, LogFiles.OpeningErrors, startedAtUnix, exportLog);
        TryAppendSeparator(dirLogs, LogFiles.ExportErrors, startedAtUnix, exportLog);
        TryAppendSeparator(dirLogs, LogFiles.VersionNotFound, startedAtUnix, exportLog);
        TryAppendSeparator(dirLogs, LogFiles.VersionTooNew, startedAtUnix, exportLog);
        TryAppendSeparator(dirLogs, LogFiles.MTimeIssues, startedAtUnix, exportLog);
    }

    /// <summary>
    /// Пытается сохранить историю и вернуть признак успеха.
    /// </summary>
    /// <param name="history">Текущее состояние истории в памяти.</param>
    /// <param name="historyWorkbookPath">
    /// Полный путь к <see cref="ProjectFileNames.HistoryWorkbook"/>.
    /// </param>
    /// <param name="sheetName">Имя листа истории.</param>
    /// <param name="exportLog">Логгер оркестратора.</param>
    /// <returns>
    /// <see langword="true"/>, если история успешно сохранена;
    /// иначе <see langword="false"/>.
    /// </returns>
    public bool TrySaveHistory(
        HistoryManager history,
        string historyWorkbookPath,
        string sheetName,
        ConsoleLogger exportLog)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(exportLog);

        try
        {
            _historyStore.Save(
                historyWorkbookPath,
                sheetName,
                history.GetRowsSnapshot());

            return true;
        }
        catch (Exception ex)
        {
            exportLog.Error(
                "Не удалось сохранить файл истории '{0}': {1}",
                historyWorkbookPath,
                ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Пишет набор строк в txt-лог без завершающего разделителя.
    /// </summary>
    /// <param name="dirLogs">Каталог текстовых логов.</param>
    /// <param name="baseName">Базовое имя лога.</param>
    /// <param name="lines">Строки для записи.</param>
    /// <param name="exportLog">Логгер текущего запуска.</param>
    /// <returns>
    /// <see langword="true"/>, если запись прошла успешно;
    /// иначе <see langword="false"/>.
    /// </returns>
    private static bool TryWriteLinesWithoutSeparator(
        string dirLogs,
        string baseName,
        IEnumerable<string> lines,
        ConsoleLogger exportLog)
    {
        try
        {
            TextLogs.WriteLines(dirLogs, baseName, lines, string.Empty);
            return true;
        }
        catch (Exception ex)
        {
            exportLog.Warn(
                "Не удалось записать диагностический txt-лог '{0}': {1}",
                baseName,
                ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Пытается дописать разделитель в конец затронутого txt-лога.
    /// </summary>
    /// <param name="dirLogs">Каталог текстовых логов.</param>
    /// <param name="baseName">Базовое имя лога.</param>
    /// <param name="startedAtUnix">Время старта текущего прогона.</param>
    /// <param name="exportLog">Логгер текущего запуска.</param>
    private static void TryAppendSeparator(
        string dirLogs,
        string baseName,
        long startedAtUnix,
        ConsoleLogger exportLog)
    {
        try
        {
            TextLogs.AppendSeparatorIfTouched(dirLogs, baseName, startedAtUnix);
        }
        catch (Exception ex)
        {
            exportLog.Warn(
                "Не удалось дописать разделитель в txt-лог '{0}': {1}",
                baseName,
                ex.Message);
        }
    }
}
