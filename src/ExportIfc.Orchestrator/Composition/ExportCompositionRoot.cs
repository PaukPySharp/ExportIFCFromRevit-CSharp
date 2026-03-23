using ExportIfc.Export;
using ExportIfc.Export.Diagnostics;
using ExportIfc.Export.Planning;
using ExportIfc.Export.Runtime;
using ExportIfc.Export.Selection;
using ExportIfc.History;
using ExportIfc.Manage;
using ExportIfc.Revit;
using ExportIfc.Settings;
using ExportIfc.Transfer;
using ExportIfc.Validation;

namespace ExportIfc.Composition;

/// <summary>
/// Composition root внешнего оркестратора.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует создание рабочих зависимостей внешнего оркестратора.
/// 2. Держит сборку графа объектов вне прикладных сервисов.
/// 3. Делает конфигурацию запуска отдельно находимой и предсказуемой.
///
/// Контракты:
/// 1. Composition root создаёт зависимости вручную и не содержит прикладной логики экспорта.
/// 2. Изменение состава зависимостей оркестратора выполняется здесь,
///    а не размазывается по вызывающему коду.
/// 3. Метод сборки возвращает полностью готовый экземпляр <see cref="ExportOrchestrator"/>.
/// </remarks>
internal static class ExportCompositionRoot
{
    /// <summary>
    /// Создаёт полностью настроенный оркестратор экспорта.
    /// </summary>
    /// <param name="settings">Загруженные настройки приложения.</param>
    /// <returns>Готовый экземпляр оркестратора.</returns>
    /// <remarks>
    /// Метод вручную связывает конкретные реализации зависимостей,
    /// используемых внешним оркестратором в текущей конфигурации приложения.
    /// </remarks>
    public static ExportOrchestrator CreateOrchestrator(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Загрузка и разбор управляющей Excel-книги.
        var manageWorkbookLoader = new ManageWorkbookLoader();

        // Рабочая история состояний моделей и служебная диагностика прогона.
        var historyStore = new HistoryWorkbookStore();
        var exportDiagnosticsWriter = new ExportDiagnosticsWriter(historyStore);

        // Проверка актуальности моделей и отбор кандидатов на выгрузку.
        var ifcFreshnessChecker = new IfcFreshnessChecker();
        var exportModelSelectionService = new ExportModelSelectionService(ifcFreshnessChecker);

        // Построение batch-плана по версиям Revit.
        var revitBatchPlanBuilder = new RevitBatchPlanBuilder();

        // Запуск Revit и обмен данными через временный transfer-файл.
        var revitExeLocator = new RevitExeLocator();
        var revitLauncher = new RevitLauncher(revitExeLocator);
        var transferStore = new TransferStore();
        var revitBatchRunner = new RevitBatchRunner(revitLauncher, transferStore);

        // Подготовка выходных каталогов перед записью IFC.
        var outputDirectoryPreparer = new OutputDirectoryPreparer();

        return new ExportOrchestrator(
            settings,
            manageWorkbookLoader,
            historyStore,
            exportDiagnosticsWriter,
            exportModelSelectionService,
            revitBatchPlanBuilder,
            revitBatchRunner,
            outputDirectoryPreparer);
    }
}