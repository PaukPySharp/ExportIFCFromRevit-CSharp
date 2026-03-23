using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

using ExportIfc.Config;
using ExportIfc.RevitAddin.Batch.Context;
using ExportIfc.RevitAddin.Composition;
using ExportIfc.RevitAddin.Logging;
using ExportIfc.Transfer;

namespace ExportIfc.RevitAddin.Batch.Runtime;

/// <summary>
/// Управляет жизненным циклом batch-выполнения add-in через событие Idling.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Ставит batch-выполнение в очередь после полной инициализации add-in.
/// 2. Гарантирует однократный запуск batch-логики в текущем процессе Revit.
/// 3. После завершения пакетной обработки повторно пытается штатно закрыть Revit.
///
/// Контракты:
/// 1. Экземпляр <see cref="RunnerEngine"/> запускает batch-логику не более одного раза за процесс.
/// 2. Выполнение стартует только после вызова <see cref="TryQueue"/>.
/// 3. После завершения batch-сценария runner запрашивает штатное закрытие Revit.
/// 4. Ошибки верхнего уровня фиксируются в техническом логе и статусе batch-запуска.
/// </remarks>
internal sealed class RunnerEngine
{
    private static readonly TimeSpan _closeRetryWindow =
        TimeSpan.FromSeconds(BatchRunThresholds.GracefulCloseWindowSeconds);

    private readonly RunnerDependencies _deps;

    // Храним именно тот объект, через который подписались на Idling,
    // чтобы потом корректно отписаться от того же источника события.
    private UIControlledApplication? _uiControlledApplication;

    // Флаг защищает от повторной постановки runner в очередь.
    private bool _started;

    // Флаг отделяет фазу batch-выполнения от фазы штатного завершения процесса:
    // обработчик Idling не должен повторно запускать export-пайплайн.
    private bool _executionFinished;

    // Закрытие может не пройти с первой попытки:
    // Revit не всегда готов принять ExitRevit в тот же цикл Idling.
    private int _closeAttempts;

    // После записи итогового статуса add-in даём Revit ограниченное окно
    // на штатное закрытие, чтобы не держать Idling в бесконечном цикле.
    private DateTime? _closePhaseStartedAtUtc;

    /// <summary>
    /// Создаёт runtime-исполнитель batch-логики add-in.
    /// </summary>
    /// <param name="deps">Набор зависимостей batch-исполнителя.</param>
    public RunnerEngine(RunnerDependencies deps)
    {
        _deps = deps ?? throw new ArgumentNullException(nameof(deps));
    }

    /// <summary>
    /// Ставит batch-исполнитель в очередь на однократный запуск через событие Idling.
    /// </summary>
    /// <param name="uiApp">Экземпляр UIControlledApplication текущего процесса Revit.</param>
    /// <remarks>
    /// Повторные вызовы после первой постановки в очередь игнорируются.
    /// </remarks>
    public void TryQueue(UIControlledApplication uiApp)
    {
        if (_started)
            return;

        _started = true;
        _uiControlledApplication = uiApp;
        uiApp.Idling += OnIdling;
    }

    /// <summary>
    /// Обрабатывает жизненный цикл batch-исполнителя на событии Idling.
    /// </summary>
    /// <param name="sender">Источник события Idling.</param>
    /// <param name="e">Аргументы события Idling.</param>
    /// <remarks>
    /// Первый рабочий заход запускает batch-логику.
    /// После завершения выполнения движок на последующих циклах Idling
    /// повторно пытается штатно закрыть Revit.
    /// </remarks>
    private void OnIdling(object? sender, IdlingEventArgs e)
    {
        if (sender is not UIApplication uiApp)
            return;

        // Пока runner ещё работает или ждёт штатного закрытия Revit,
        // просим Revit не возвращаться к default-частоте Idling.
        // Это убирает сценарий, когда пакет уже завершён, а следующая idle-сессия
        // долго не наступает из-за отсутствия пользовательской активности.
        e.SetRaiseWithoutDelay();

        if (!_executionFinished)
        {
            // Batch-логика должна выполниться только один раз.
            // Пока она не завершена, используем первый подходящий цикл Idling как точку старта.
            ExecuteOnce(uiApp);
            return;
        }

        if (_closePhaseStartedAtUtc is null)
            _closePhaseStartedAtUtc = DateTime.UtcNow;

        if (DateTime.UtcNow - _closePhaseStartedAtUtc.Value >= _closeRetryWindow)
        {
            var environment = BatchRunEnvironmentSnapshot.Read();
            WriteCloseAttempt(
                environment.DirAdminData,
                _closeAttempts,
                $"Исчерпано окно штатного закрытия Revit ({(int)_closeRetryWindow.TotalSeconds} сек.). Дальнейшие попытки прекращены.");
            DetachIdling();
            return;
        }

        _closeAttempts++;

        if (TryCloseRevit(uiApp, _closeAttempts))
            DetachIdling();
    }

    /// <summary>
    /// Выполняет batch-логику один раз и переводит runner в фазу штатного закрытия Revit.
    /// </summary>
    /// <param name="uiApp">Экземпляр UIApplication текущей UI-сессии Revit.</param>
    private void ExecuteOnce(UIApplication uiApp)
    {
        try
        {
            Execute(uiApp);
        }
        catch (Exception ex)
        {
            AddinLogs.TryWriteFatal(ex);
            AddinLogs.TryWriteRunStatusFromEnv(
                BatchRunStatuses.Failed,
                "Необработанное исключение верхнего уровня в batch-исполнителе add-in.");
        }
        finally
        {
            // Фаза выполнения считается завершённой всегда, даже при ошибке.
            // Это не даёт повторно запускать пакет на следующих Idling.
            _executionFinished = true;
        }
    }

    /// <summary>
    /// Выполняет batch-пакет внутри текущей сессии Revit.
    /// </summary>
    /// <param name="uiApp">Экземпляр UIApplication текущей UI-сессии Revit.</param>
    private void Execute(UIApplication uiApp)
    {
        if (!_deps.BatchRunContextReader.TryRead(out var context) || context is null)
            return;

        WriteStartupSession(context);

        if (!_deps.BatchRunInputLoader.TryLoad(context, out var input) || input is null)
            return;

        var result = _deps.BatchExecutor.Execute(uiApp, context, input);

        AddinLogs.TryWriteRunStatus(
            context.DirAdminData,
            result.FinalStatus,
            result.FinalMessage);

        AddinLogs.WriteStartup(
            context.DirAdminData,
            $"Обработка пакета завершена. Итоговый статус: {result.FinalStatus}. Запрошено штатное закрытие Revit.");
    }

    /// <summary>
    /// Отписывает batch-исполнитель от события Idling.
    /// </summary>
    private void DetachIdling()
    {
        if (_uiControlledApplication is null)
            return;

        _uiControlledApplication.Idling -= OnIdling;
        _uiControlledApplication = null;
    }

    /// <summary>
    /// Открывает startup-сессию технического лога batch-запуска.
    /// </summary>
    /// <param name="context">Контекст текущего batch-запуска.</param>
    private static void WriteStartupSession(BatchRunContext context)
    {
        AddinLogs.BeginStartupSession(
            context.DirAdminData,
            $"Среда Revit: '{AppDomain.CurrentDomain.BaseDirectory}'",
            $"Сборка add-in: '{typeof(App).Assembly.Location}'",
            $"Сборка common: '{typeof(TransferStore).Assembly.Location}'");
    }

    /// <summary>
    /// Пытается штатно поставить команду закрытия Revit в очередь.
    /// </summary>
    /// <param name="uiApp">Экземпляр UIApplication текущей UI-сессии Revit.</param>
    /// <param name="attempt">Номер текущей попытки закрытия.</param>
    /// <returns>
    /// <see langword="true"/>, если команда закрытия поставлена в очередь;
    /// иначе — <see langword="false"/>.
    /// </returns>
    private static bool TryCloseRevit(UIApplication uiApp, int attempt)
    {
        var environment = BatchRunEnvironmentSnapshot.Read();
        var dirAdminData = environment.DirAdminData;

        try
        {
            var closeCommandId =
                RevitCommandId.LookupPostableCommandId(PostableCommand.ExitRevit);

            if (closeCommandId is null)
            {
                WriteCloseAttempt(dirAdminData, attempt, "команда ExitRevit не найдена.");
                return false;
            }

            if (!uiApp.CanPostCommand(closeCommandId))
            {
                WriteCloseAttempt(dirAdminData, attempt, "Revit пока не готов принять команду ExitRevit.");
                return false;
            }

            uiApp.PostCommand(closeCommandId);

            if (attempt > 1)
            {
                WriteCloseAttempt(dirAdminData, attempt, "команда ExitRevit поставлена в очередь.");
            }

            return true;
        }
        catch (Exception ex)
        {
            WriteCloseAttempt(
                dirAdminData,
                attempt,
                $"не удалось поставить команду закрытия Revit ({ex}).");

            return false;
        }
    }

    /// <summary>
    /// Пишет в startup-лог информацию о попытке штатного закрытия Revit.
    /// </summary>
    /// <param name="dirAdminData">Рабочая директория admin-data текущего запуска.</param>
    /// <param name="attempt">Номер попытки закрытия.</param>
    /// <param name="message">Текст сообщения о результате попытки.</param>
    private static void WriteCloseAttempt(string dirAdminData, int attempt, string message)
    {
        if (string.IsNullOrWhiteSpace(dirAdminData))
            return;

        AddinLogs.WriteStartup(dirAdminData, $"Попытка #{attempt}: {message}");
    }
}
