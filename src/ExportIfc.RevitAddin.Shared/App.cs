using Autodesk.Revit.UI;

using ExportIfc.Config;
using ExportIfc.RevitAddin.Logging;
using ExportIfc.RevitAddin.Startup;
using ExportIfc.RevitAddin.Startup.Dialogs;

namespace ExportIfc.RevitAddin;

/// <summary>
/// Точка входа add-in внутри Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Подписывает batch-autorun на момент полной инициализации Revit.
/// 2. Подключает session-wide guard UI-шумов для batch-сценария.
/// 3. Не запускает batch-логику вне autorun-сценария оркестратора.
///
/// Контракты:
/// 1. Add-in ставит batch-исполнитель в очередь только при наличии batch-флага запуска.
/// 2. Основная логика не стартует до события ApplicationInitialized.
/// 3. UI-guard подключается только для batch-запуска add-in.
/// 4. Ошибки ранней инициализации фиксируются в техническом логе и статусе запуска.
/// </remarks>
public sealed class App : IExternalApplication
{
    /// <summary>
    /// Выполняется при загрузке add-in.
    /// </summary>
    /// <param name="application">Экземпляр UIControlledApplication.</param>
    /// <returns>Результат инициализации add-in.</returns>
    public Result OnStartup(UIControlledApplication application)
    {
        if (!BatchLaunchDetector.IsBatchLaunchRequested())
            return Result.Succeeded;

        RevitUiNoiseGuard.Attach(application);

        application.ControlledApplication.ApplicationInitialized +=
            (_, __) =>
            {
                try
                {
                    // В Debug add-in может поставить раннюю паузу,
                    // чтобы успеть подключить debugger до старта batch-логики.
                    // Ожидание включается только если оркестратор явно передал соответствующий env-флаг.
                    DebugAttachGate.WaitIfRequested();

                    Runner.TryQueue(application);
                }
                catch (Exception ex)
                {
                    AddinLogs.TryWriteFatal(ex);
                    AddinLogs.TryWriteRunStatusFromEnv(
                        BatchRunStatuses.Failed,
                        "Сбой на этапе инициализации add-in до постановки batch-исполнителя в очередь.");
                }
            };

        return Result.Succeeded;
    }

    /// <summary>
    /// Выполняется при выгрузке add-in.
    /// </summary>
    /// <param name="application">Экземпляр UIControlledApplication.</param>
    /// <returns>Результат завершения работы add-in.</returns>
    /// <remarks>
    /// Метод отключает session-wide обработчики UI-шумов,
    /// если они были подключены для batch-сценария.
    /// </remarks>
    public Result OnShutdown(UIControlledApplication application)
    {
        RevitUiNoiseGuard.Detach(application);
        return Result.Succeeded;
    }
}