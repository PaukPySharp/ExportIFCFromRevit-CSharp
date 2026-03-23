using ExportIfc.RevitAddin.Batch.Context;
using ExportIfc.RevitAddin.Batch.Export;
using ExportIfc.RevitAddin.Batch.Input;
using ExportIfc.RevitAddin.Batch.Runtime;

namespace ExportIfc.RevitAddin.Composition;

/// <summary>
/// Набор зависимостей batch-runner внутри процесса Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Хранит итоговый набор runtime-зависимостей batch-исполнителя add-in.
/// 2. Передаёт вручную собранные сервисы из composition root в <see cref="RunnerEngine"/>.
///
/// Контракты:
/// 1. Экземпляр создаётся только с полностью переданным набором зависимостей.
/// 2. Класс не содержит прикладной логики и используется как контейнер runtime-сервисов.
/// </remarks>
internal sealed class RunnerDependencies
{
    /// <summary>
    /// Создаёт контейнер зависимостей batch-исполнителя.
    /// </summary>
    /// <param name="batchRunContextReader">Сервис чтения контекста пакетного запуска.</param>
    /// <param name="batchRunInputLoader">Сервис загрузки входных данных пакетного запуска.</param>
    /// <param name="batchExecutor">Сервис исполнения batch-пакета.</param>
    public RunnerDependencies(
        BatchRunContextReader batchRunContextReader,
        BatchRunInputLoader batchRunInputLoader,
        BatchExecutor batchExecutor)
    {
        BatchRunContextReader = batchRunContextReader ?? throw new ArgumentNullException(nameof(batchRunContextReader));
        BatchRunInputLoader = batchRunInputLoader ?? throw new ArgumentNullException(nameof(batchRunInputLoader));
        BatchExecutor = batchExecutor ?? throw new ArgumentNullException(nameof(batchExecutor));
    }

    /// <summary>
    /// Сервис чтения контекста пакетного запуска.
    /// </summary>
    public BatchRunContextReader BatchRunContextReader { get; }

    /// <summary>
    /// Сервис загрузки входных данных пакетного запуска.
    /// </summary>
    public BatchRunInputLoader BatchRunInputLoader { get; }

    /// <summary>
    /// Сервис исполнения batch-пакета.
    /// </summary>
    public BatchExecutor BatchExecutor { get; }
}