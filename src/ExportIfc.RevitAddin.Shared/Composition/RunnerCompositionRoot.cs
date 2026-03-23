using ExportIfc.RevitAddin.Batch.Context;
using ExportIfc.RevitAddin.Batch.Export;
using ExportIfc.RevitAddin.Batch.Input;
using ExportIfc.RevitAddin.Revit;
using ExportIfc.Transfer;

namespace ExportIfc.RevitAddin.Composition;

/// <summary>
/// Composition root batch-исполнителя внутри процесса Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует ручную сборку runtime-зависимостей add-in.
/// 2. Держит wiring зависимостей вне точки входа и runtime-исполнителя.
///
/// Контракты:
/// 1. Не содержит прикладной логики batch-обработки.
/// 2. Возвращает полностью собранный набор зависимостей <see cref="RunnerDependencies"/>.
/// </remarks>
internal static class RunnerCompositionRoot
{
    /// <summary>
    /// Создаёт набор зависимостей runtime-исполнителя add-in.
    /// </summary>
    /// <returns>Полностью собранный набор зависимостей batch-runner.</returns>
    /// <remarks>
    /// Метод вручную связывает конкретные реализации,
    /// используемые batch-runner внутри процесса Revit.
    /// </remarks>
    public static RunnerDependencies Create()
    {
        // Чтение контекста и входных данных пакетного запуска.
        var transferStore = new TransferStore();
        var batchRunContextReader = new BatchRunContextReader();
        var batchRunInputLoader = new BatchRunInputLoader(transferStore);

        // Платформенно-зависимая сборка IFC-опций и выполнение экспорта моделей.
        var ifcExportOptionsFactory = new IfcExportOptionsFactory();
        var modelExportExecutor = new ModelExportExecutor(ifcExportOptionsFactory);
        var batchExecutor = new BatchExecutor(modelExportExecutor);

        return new RunnerDependencies(
            batchRunContextReader,
            batchRunInputLoader,
            batchExecutor);
    }
}