using ExportIfc.Config;
using ExportIfc.Export.Planning;
using ExportIfc.Export.Runtime;
using ExportIfc.Export.Selection;
using ExportIfc.Export.Diagnostics;
using ExportIfc.History;
using ExportIfc.Logging;
using ExportIfc.Manage;
using ExportIfc.Models;
using ExportIfc.Settings;

namespace ExportIfc.Export;

/// <summary>
/// Оркестратор пакетного экспорта IFC.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Координирует полный цикл пакетной выгрузки: от чтения управляющей Excel-таблицы
///    до подготовки и выполнения batch-пакетов Revit.
/// 2. Централизует последовательность шагов внешнего прогона,
///    не размазывая orchestration-логику по отдельным сервисам.
/// 3. Обеспечивает единое место для логирования, подготовки временных данных
///    и финализации результата.
///
/// Контракты:
/// 1. Источник моделей — управляющая Excel-таблица.
/// 2. В real-run используется один общий временный файл
///    <see cref="ProjectFileNames.TmpJson"/>:
///    перед каждым запуском очередного batch-пакета он перезаписывается,
///    а в финализации прогона удаляется.
/// 3. В dry-run общий <see cref="ProjectFileNames.TmpJson"/> не используется;
///    вместо него сохраняются пер-версионные debug JSON-файлы пакетов.
/// 4. История сохраняется по итогам прогона даже при частичных ошибках.
/// 5. Код завершения 0 означает успешный прогон, 1 — наличие ошибок.
/// </remarks>
public sealed class ExportOrchestrator
{
    private readonly AppSettings _stg;
    private readonly IManageWorkbookLoader _manageWorkbookLoader;
    private readonly IHistoryStore _historyStore;
    private readonly ExportModelSelectionService _exportModelSelectionService;
    private readonly RevitBatchPlanBuilder _revitBatchPlanBuilder;
    private readonly RevitBatchRunner _revitBatchRunner;
    private readonly ExportDiagnosticsWriter _exportDiagnosticsWriter;
    private readonly OutputDirectoryPreparer _outputDirectoryPreparer;

    /// <summary>
    /// Создаёт оркестратор экспорта с явно переданными зависимостями.
    /// </summary>
    /// <param name="stg">Итоговые настройки текущего запуска.</param>
    /// <param name="manageWorkbookLoader">Чтение управляющей Excel-книги.</param>
    /// <param name="historyStore">Чтение и сохранение рабочей истории состояний моделей.</param>
    /// <param name="exportDiagnosticsWriter">Запись диагностических и итоговых артефактов прогона.</param>
    /// <param name="exportModelSelectionService">Отбор моделей для выгрузки.</param>
    /// <param name="revitBatchPlanBuilder">Построение batch-плана по версиям Revit.</param>
    /// <param name="revitBatchRunner">Подготовка и выполнение batch-пакетов Revit.</param>
    /// <param name="outputDirectoryPreparer">Подготовка выходных каталогов перед выгрузкой.</param>
    internal ExportOrchestrator(
        AppSettings stg,
        IManageWorkbookLoader manageWorkbookLoader,
        IHistoryStore historyStore,
        ExportDiagnosticsWriter exportDiagnosticsWriter,
        ExportModelSelectionService exportModelSelectionService,
        RevitBatchPlanBuilder revitBatchPlanBuilder,
        RevitBatchRunner revitBatchRunner,
        OutputDirectoryPreparer outputDirectoryPreparer)
    {
        ArgumentNullException.ThrowIfNull(stg);
        ArgumentNullException.ThrowIfNull(manageWorkbookLoader);
        ArgumentNullException.ThrowIfNull(historyStore);
        ArgumentNullException.ThrowIfNull(exportDiagnosticsWriter);
        ArgumentNullException.ThrowIfNull(exportModelSelectionService);
        ArgumentNullException.ThrowIfNull(revitBatchPlanBuilder);
        ArgumentNullException.ThrowIfNull(revitBatchRunner);
        ArgumentNullException.ThrowIfNull(outputDirectoryPreparer);

        _stg = stg;
        _manageWorkbookLoader = manageWorkbookLoader;
        _historyStore = historyStore;
        _exportDiagnosticsWriter = exportDiagnosticsWriter;
        _exportModelSelectionService = exportModelSelectionService;
        _revitBatchPlanBuilder = revitBatchPlanBuilder;
        _revitBatchRunner = revitBatchRunner;
        _outputDirectoryPreparer = outputDirectoryPreparer;
    }

    /// <summary>
    /// Выполняет полный цикл подготовки и запуска экспорта.
    /// </summary>
    /// <returns>Код завершения процесса.</returns>
    /// <remarks>
    /// Метод создаёт контекст запуска, выполняет основной прогон,
    /// затем в обязательном порядке завершает работу с временными данными
    /// и пытается сохранить историю.
    /// </remarks>
    public int Run()
    {
        var context = ExportRunContext.Create(_stg);

        // Зеркало консоли привязывается к per-run txt-логу уже после того,
        // как у запуска появились runtime-пути и уникальный runId.
        ConsoleTranscript.Initialize(
            ProjectFiles.OrchestratorConsoleLog(context.Paths.DirAdminData, context.RunId));

        BeginRun(context);

        HistoryManager? history = null;
        var anyFailures = false;
        var historySaved = false;

        try
        {
            // Сначала собираем исходные данные прогона:
            // список моделей из управляющей workbook и рабочую историю состояний моделей.
            var models = LoadModels(context);
            history = LoadHistory(context);

            // После отбора остаются только модели, которые действительно нужно экспортировать сейчас.
            var selection = SelectModels(context, models, history);

            // План группирует модели по версиям запуска Revit
            // и одновременно накапливает диагностику по неподдерживаемым версиям.
            var batchPlan = _revitBatchPlanBuilder.Build(
                selection.ModelsToExport,
                context.Settings.RevitVersions,
                history);

            // Выходные каталоги готовятся до batch-этапа,
            // чтобы ошибки файловой подготовки не размазывались по пакетам.
            _outputDirectoryPreparer.EnsureFor(batchPlan.Batches.SelectMany(batch => batch.Models));

            // Диагностика по версиям пишется после построения плана,
            // когда уже известны и реальные пакеты, и отсеянные модели.
            _exportDiagnosticsWriter.WriteVersionDiagnostics(context, batchPlan, context.ExportLog);

            var planningFailures = ReportPlanningFailures(context, batchPlan);
            anyFailures = _revitBatchRunner.Execute(context, batchPlan) || planningFailures;
        }
        catch (Exception ex)
        {
            // Оркестратор завершает прогон управляемо:
            // ошибка фиксируется в логе и переводит итог в неуспешный код возврата.
            anyFailures = true;
            context.ExportLog.Error(
                "Выгрузка прервана необработанным исключением:{0}{1}",
                Environment.NewLine,
                ex);
        }
        finally
        {
            FinishRun(context);

            if (history is not null)
            {
                historySaved = _exportDiagnosticsWriter.TrySaveHistory(
                    history,
                    context.HistoryWorkbookPath,
                    context.Settings.SheetHistory,
                    context.ExportLog);
            }
            else
            {
                context.ExportLog.Warn(
                    "История не была сохранена: состояние истории не успело загрузиться.");
            }
        }

        return BuildExitCode(context, anyFailures, historySaved);
    }

    /// <summary>
    /// Переводит диагностические проблемы batch-плана в явный признак ошибки прогона.
    /// </summary>
    /// <param name="context">Контекст текущего запуска.</param>
    /// <param name="batchPlan">Построенный batch-план.</param>
    /// <returns>
    /// <see langword="true"/>, если часть моделей была исключена из batch-обработки
    /// из-за проблем с определением версии Revit; иначе <see langword="false"/>.
    /// </returns>
    private static bool ReportPlanningFailures(
        ExportRunContext context,
        RevitBatchPlan batchPlan)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(batchPlan);

        var versionNotFoundCount = batchPlan.VersionNotFound.Count;
        var versionTooNewCount = batchPlan.VersionTooNew.Count;

        if (versionNotFoundCount == 0 && versionTooNewCount == 0)
            return false;

        context.ExportLog.Warn(
            "Часть моделей исключена из batch-обработки из-за проблем с определением версии Revit. " +
            "VersionNotFound: {0} | VersionTooNew: {1}.",
            versionNotFoundCount,
            versionTooNewCount);

        return true;
    }

    /// <summary>
    /// Выполняет стартовую подготовку прогона и пишет вводную информацию в лог.
    /// </summary>
    /// <param name="context">Контекст текущего запуска.</param>
    /// <remarks>
    /// Перед стартом очищаются служебные JSON-артефакты предыдущего прогона:
    /// общий <see cref="ProjectFileNames.TmpJson"/> и dry-run JSON-файлы пакетов.
    /// Это не даёт пользователю спутать свежие результаты со старыми хвостами.
    /// </remarks>
    private void BeginRun(ExportRunContext context)
    {
        Log.Rule("Подготовка данных");

        context.ExportLog.Info(
            "Настройки загружены.\nКаталог конфигураций экспорта: '{0}',\n{1}: '{2}'",
            context.Paths.DirExportConfig,
            ProjectDirectoryNames.AdminData,
            context.Paths.DirAdminData);

        context.ExportLog.Info(
            "Управляющая Excel-таблица экспорта IFC: '{0}'",
            context.ManageWorkbookPath);

        TryDeleteTmpJson(context.TmpJsonPath, context.ExportLog);
        TryDeleteDryRunTransferJsonFiles(context.Paths.DirAdminData, context.ExportLog);

        if (context.Settings.RunRevit)
            return;

        context.ExportLog.Warn(
            "Включён dry-run. Revit запускаться не будет: будут подготовлены {0}, debug JSON-файлы пакетов и файл истории {1}.",
            ProjectFileNames.TaskFileDisplayName,
            ProjectFileNames.HistoryWorkbook);
    }

    /// <summary>
    /// Загружает рабочую историю состояний моделей.
    /// </summary>
    /// <param name="context">Контекст текущего запуска.</param>
    /// <returns>Менеджер истории, построенный по данным workbook-файла.</returns>
    private HistoryManager LoadHistory(ExportRunContext context)
    {
        Log.Rule("Проверка актуальности");

        var historyRows = _historyStore.ReadRows(
            context.HistoryWorkbookPath,
            context.Settings.SheetHistory);

        return HistoryManager.FromRows(historyRows);
    }

    /// <summary>
    /// Отбирает модели, которые действительно нужно выгружать в текущем прогоне.
    /// </summary>
    /// <param name="context">Контекст текущего запуска.</param>
    /// <param name="models">Модели после чтения управляющей таблицы и ignore-фильтрации.</param>
    /// <param name="history">Рабочая история состояний моделей.</param>
    /// <returns>Результат отбора моделей для экспорта.</returns>
    private ExportSelectionResult SelectModels(
        ExportRunContext context,
        IReadOnlyCollection<RevitModel> models,
        HistoryManager history)
    {
        return _exportModelSelectionService.SelectModelsToExport(
            models,
            history,
            context.ExportLog);
    }

    /// <summary>
    /// Загружает модели из управляющей Excel-книги и применяет лист ignore.
    /// </summary>
    /// <param name="context">Контекст текущего запуска.</param>
    /// <returns>Список моделей, допущенных к дальнейшей проверке.</returns>
    /// <remarks>
    /// Помимо списка моделей метод пишет диагностические замечания по mtime
    /// и исключает пути, явно перечисленные на листе ignore.
    /// </remarks>
    private List<RevitModel> LoadModels(ExportRunContext context)
    {
        var data = _manageWorkbookLoader.Load(
            context.ManageWorkbookPath,
            context.Settings,
            context.Paths);

        context.ExportLog.Info("В управляющей таблице найдено моделей: {0}", data.Models.Count);

        _exportDiagnosticsWriter.WriteMTimeIssues(
            context.Paths.DirLogs,
            data.MTimeIssues,
            context.ExportLog);

        if (data.Ignore.Count == 0)
            return data.Models;

        var before = data.Models.Count;
        var filtered = data.Models
            .Where(model => !data.Ignore.Contains(model.RvtPath))
            .ToList();

        var excludedByIgnore = before - filtered.Count;

        context.ExportLog.Info(
            "Исключено по листу ignore: {0}. Осталось моделей для проверки: {1}",
            excludedByIgnore,
            filtered.Count);

        return filtered;
    }

    /// <summary>
    /// Завершает прогон и дописывает финальные служебные артефакты.
    /// </summary>
    /// <param name="context">Контекст текущего запуска.</param>
    /// <remarks>
    /// Метод не сохраняет историю: этот шаг выполняется отдельно,
    /// после завершения основной части прогона.
    /// </remarks>
    private void FinishRun(ExportRunContext context)
    {
        FinishTmpJsonHandling(context);
        _exportDiagnosticsWriter.AppendTouchedSeparators(context, context.ExportLog);
    }

    /// <summary>
    /// Формирует итоговый код завершения и пишет финальное сообщение в лог.
    /// </summary>
    /// <param name="context">Контекст текущего запуска.</param>
    /// <param name="anyFailures">Признак ошибок при batch-этапе.</param>
    /// <param name="historySaved">Признак успешного сохранения истории.</param>
    /// <returns>0 при полном успехе; 1 при любой ошибке.</returns>
    private static int BuildExitCode(
        ExportRunContext context,
        bool anyFailures,
        bool historySaved)
    {
        if (anyFailures || !historySaved)
        {
            context.ExportLog.Error(
                "Выгрузка завершена с ошибками. Status=Failed | Ошибки прогона: {0} | История сохранена: {1}.",
                anyFailures,
                historySaved);
            return 1;
        }

        context.ExportLog.Info("Обработка завершена.");
        return 0;
    }

    /// <summary>
    /// Завершает работу с JSON-артефактами передачи по правилам текущего режима запуска.
    /// </summary>
    /// <param name="context">Контекст текущего запуска.</param>
    /// <remarks>
    /// В real-run общий <see cref="ProjectFileNames.TmpJson"/> удаляется как временный transport-файл.
    /// В dry-run общий <see cref="ProjectFileNames.TmpJson"/> не используется,
    /// а пер-версионные debug JSON-файлы пакетов сохраняются на диске.
    /// </remarks>
    private static void FinishTmpJsonHandling(ExportRunContext context)
    {
        if (context.Settings.RunRevit)
        {
            TryDeleteTmpJson(context.TmpJsonPath, context.ExportLog);
            return;
        }

        context.ExportLog.Info(
            "Dry-run завершён. Debug JSON-файлы пакетов оставлены: '{0}'",
            context.Paths.DirAdminData);
    }

    /// <summary>
    /// Пытается удалить общий временный JSON-файл передачи без прерывания основного сценария.
    /// </summary>
    /// <param name="tmpJsonPath">Полный путь к временному JSON-файлу.</param>
    /// <param name="exportLog">Лог текущего прогона.</param>
    /// <remarks>
    /// Ошибка удаления рассматривается как диагностическая,
    /// а не как причина аварийного завершения оркестратора.
    /// </remarks>
    private static void TryDeleteTmpJson(string tmpJsonPath, ConsoleLogger exportLog)
    {
        try
        {
            if (!File.Exists(tmpJsonPath))
                return;

            File.Delete(tmpJsonPath);
            exportLog.Info("Удалён временный JSON-файл передачи: '{0}'", tmpJsonPath);
        }
        catch (Exception ex)
        {
            exportLog.Warn("Не удалось удалить временный JSON-файл передачи '{0}': {1}", tmpJsonPath, ex.Message);
        }
    }

    /// <summary>
    /// Пытается удалить dry-run JSON-файлы пакетов от предыдущего прогона.
    /// </summary>
    /// <param name="dirAdminData">Каталог <see cref="ProjectDirectoryNames.AdminData"/>.</param>
    /// <param name="exportLog">Лог текущего запуска.</param>
    /// <remarks>
    /// Очистка предотвращает путаницу между свежими и устаревшими debug JSON-файлами.
    /// Ошибки удаления логируются, но не срывают запуск.
    /// </remarks>
    private static void TryDeleteDryRunTransferJsonFiles(string dirAdminData, ConsoleLogger exportLog)
    {
        try
        {
            var searchPattern = ProjectFileNames.TmpDryRunFilePrefix + "*" + ProjectFileExtensions.Json;
            var files = Directory.GetFiles(dirAdminData, searchPattern);

            if (files.Length == 0)
                return;

            foreach (var filePath in files)
                File.Delete(filePath);

            exportLog.Info("Удалены dry-run JSON-файлы пакетов от предыдущего прогона: {0} шт.", files.Length);
        }
        catch (Exception ex)
        {
            exportLog.Warn("Не удалось очистить dry-run JSON-файлы пакетов: {0}", ex.Message);
        }
    }
}
