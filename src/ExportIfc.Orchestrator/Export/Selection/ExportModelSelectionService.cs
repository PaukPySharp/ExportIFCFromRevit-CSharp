using ExportIfc.History;
using ExportIfc.Logging;
using ExportIfc.Models;
using ExportIfc.Validation;

namespace ExportIfc.Export.Selection;

/// <summary>
/// Прикладной сервис отбора моделей, которые действительно требуют экспорта.
/// </summary>
/// <remarks>
/// Назначение:
/// Сравнивает состояние истории и актуальность IFC-файлов,
/// после чего формирует итоговый список моделей для передачи в batch-план.
///
/// Контракты:
/// 1. Модель пропускается, только если одновременно актуальны история и все требуемые IFC.
/// 2. Проверка mapped- и nomap-направлений делегируется <see cref="IIfcFreshnessChecker"/>.
/// 3. Для частичного перевыгруза в batch-план формируется отдельная проекция модели
///    без мутации исходного экземпляра.
/// 4. Количество пропущенных актуальных моделей возвращается отдельно.
/// </remarks>
internal sealed class ExportModelSelectionService
{
    private readonly IIfcFreshnessChecker _ifcFreshnessChecker;

    /// <summary>
    /// Создаёт сервис отбора моделей с явно переданной проверкой актуальности IFC.
    /// </summary>
    /// <param name="ifcFreshnessChecker">Сервис проверки актуальности IFC.</param>
    internal ExportModelSelectionService(IIfcFreshnessChecker ifcFreshnessChecker)
    {
        ArgumentNullException.ThrowIfNull(ifcFreshnessChecker);
        _ifcFreshnessChecker = ifcFreshnessChecker;
    }

    /// <summary>
    /// Проверяет актуальность моделей и формирует итоговый список на выгрузку.
    /// </summary>
    /// <param name="models">Исходные модели после загрузки и ignore-фильтра.</param>
    /// <param name="history">Рабочая история состояний моделей.</param>
    /// <param name="exportLog">Логгер оркестратора.</param>
    /// <returns>Результат отбора моделей.</returns>
    public ExportSelectionResult SelectModelsToExport(
        IReadOnlyCollection<RevitModel> models,
        HistoryManager history,
        ConsoleLogger exportLog)
    {
        var toExport = new List<RevitModel>();
        var skippedAsActual = 0;

        foreach (var model in models)
        {
            var historyOk = history.IsUpToDate(model);
            var ifcMappingOk = _ifcFreshnessChecker.IsIfcUpToDateMapping(model);
            var ifcNoMapOk = _ifcFreshnessChecker.IsIfcUpToDateNoMap(model);

            if (historyOk && ifcMappingOk && ifcNoMapOk)
            {
                skippedAsActual++;
                continue;
            }

            if (!historyOk)
            {
                // Устаревшая или удалённая запись history — это осознанный
                // сигнал на полноценную перевыгрузку по всем настроенным маршрутам.
                toExport.Add(model);
                continue;
            }

            toExport.Add(model.CreateExportProjection(ifcMappingOk, ifcNoMapOk));
        }

        exportLog.Info(
            "К выгрузке: {0}. Уже актуальны, пропущены: {1}",
            toExport.Count,
            skippedAsActual);

        return new ExportSelectionResult(toExport, skippedAsActual);
    }
}
