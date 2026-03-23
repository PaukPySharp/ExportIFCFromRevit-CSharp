using Autodesk.Revit.UI;

using ExportIfc.RevitAddin.Batch.Runtime;
using ExportIfc.RevitAddin.Composition;

namespace ExportIfc.RevitAddin.Startup;

/// <summary>
/// Стартовая точка входа batch-исполнителя внутри процесса Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Скрывает создание session-wide runtime-исполнителя add-in.
/// 2. Даёт точке входа <see cref="App"/> минимальный контракт запуска.
///
/// Контракты:
/// 1. Использует один экземпляр <see cref="RunnerEngine"/> на процесс Revit.
/// 2. Делегирует постановку batch-исполнителя в очередь через событие Idling.
/// </remarks>
internal static class Runner
{
    private static readonly RunnerEngine _engine = new(RunnerCompositionRoot.Create());

    /// <summary>
    /// Ставит batch-исполнитель add-in в очередь на однократный запуск.
    /// </summary>
    /// <param name="uiApp">Экземпляр UIControlledApplication текущего процесса Revit.</param>
    public static void TryQueue(UIControlledApplication uiApp)
        => _engine.TryQueue(uiApp);
}
