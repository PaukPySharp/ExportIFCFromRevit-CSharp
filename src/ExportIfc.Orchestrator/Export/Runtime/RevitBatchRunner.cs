using ExportIfc.Config;
using ExportIfc.Export.Planning;
using ExportIfc.Logging;
using ExportIfc.Revit;
using ExportIfc.Settings.Loading;
using ExportIfc.Transfer;

namespace ExportIfc.Export.Runtime;

/// <summary>
/// Выполнение или подготовка пакетных запусков Revit по сформированному плану.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Проходит по batch-плану и обрабатывает каждый пакет моделей по версии Revit.
/// 2. Подготавливает Task-файлы и JSON-пакеты передачи.
/// 3. В real-run запускает дочерние процессы Revit, а в dry-run ограничивается подготовкой артефактов.
///
/// Контракты:
/// 1. В real-run используется общий <see cref="ProjectFileNames.TmpJson"/>.
/// 2. В dry-run общий <see cref="ProjectFileNames.TmpJson"/> не используется;
///    вместо него сохраняются пер-версионные debug JSON-файлы пакетов.
/// 3. Ошибка обработки конкретного пакета переводит общий результат в неуспешный,
///    но не останавливает обработку остальных пакетов.
/// </remarks>
internal sealed class RevitBatchRunner
{
    private readonly IRevitLauncher _revitLauncher;
    private readonly ITransferStore _transferStore;

    /// <summary>
    /// Создаёт сервис выполнения пакетных запусков Revit.
    /// </summary>
    /// <param name="revitLauncher">Запуск и ожидание Revit-процесса.</param>
    /// <param name="transferStore">Чтение и запись служебных файлов передачи.</param>
    internal RevitBatchRunner(
        IRevitLauncher revitLauncher,
        ITransferStore transferStore)
    {
        ArgumentNullException.ThrowIfNull(revitLauncher);
        ArgumentNullException.ThrowIfNull(transferStore);

        _revitLauncher = revitLauncher;
        _transferStore = transferStore;
    }

    /// <summary>
    /// Выполняет batch-этап текущего прогона.
    /// </summary>
    /// <param name="context">Контекст текущего запуска.</param>
    /// <param name="batchPlan">Уже построенный план пакетной обработки.</param>
    /// <returns>
    /// <see langword="true"/>, если в ходе обработки был хотя бы один неуспешный пакет;
    /// иначе <see langword="false"/>.
    /// </returns>
    public bool Execute(
        ExportRunContext context,
        RevitBatchPlan batchPlan)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(batchPlan);

        var anyFailures = false;

        if (batchPlan.HasBatches)
        {
            Log.Rule(
                context.Settings.RunRevit
                    ? "Запуск пакетов Revit"
                    : "Подготовка пакетов Revit");
        }

        // основной цикл по сериям выгрузок (готовые задачи)
        foreach (var batch in batchPlan.Batches)
        {
            if (!ExecuteBatch(context, batch))
                anyFailures = true;
        }

        return anyFailures;
    }

    /// <summary>
    /// Выполняет или подготавливает один пакет моделей для конкретной версии Revit.
    /// </summary>
    /// <param name="context">Контекст текущего запуска.</param>
    /// <param name="batch">Пакет моделей одной версии Revit.</param>
    /// <returns>
    /// <see langword="true"/>, если пакет успешно обработан;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// В real-run метод пишет общий <see cref="ProjectFileNames.TmpJson"/> и запускает Revit.
    /// В dry-run метод пишет Task-файл и отдельный debug JSON-файл пакета,
    /// но не запускает дочерний процесс.
    /// Любая ошибка подготовки артефактов или запуска пакета логируется как ошибка пакета
    /// и не прерывает обработку остальных версий.
    /// </remarks>
    private bool ExecuteBatch(
        ExportRunContext context,
        RevitBatchPlanItem batch)
    {
        try
        {
            Log.Rule($"Пакет Revit {batch.RevitMajor} • моделей {batch.Models.Count}");

            var transferEnvelope = _transferStore.BuildEnvelope(
                batch.RevitMajor,
                context.RunId,
                batch.Models);

            var taskPath = ProjectFiles.TaskFile(context.Paths, batch.RevitMajor);

            // Task-файл пишется всегда:
            // и для реального запуска Revit, и для dry-run диагностики пакета.
            _transferStore.WriteTaskModels(taskPath, batch.Models.Select(model => model.RvtPath));

            if (!context.Settings.RunRevit)
            {
                // В dry-run пакет сохраняется в отдельный per-version JSON,
                // чтобы не перетирать общий tmp.json и оставить диагностический артефакт.
                var dryRunTransferPath = ProjectFiles.DryRunTransferJson(
                    context.Paths,
                    batch.RevitMajor);

                _transferStore.WriteEnvelope(dryRunTransferPath, transferEnvelope);

                context.ExportLog.Info(
                    "Dry-run для Revit {0}. Подготовлены {1} и {2}, запуск Revit пропущен.",
                    batch.RevitMajor,
                    Path.GetFileName(taskPath),
                    Path.GetFileName(dryRunTransferPath));

                return true;
            }

            // В real-run все версии передаются через один общий tmp.json;
            // перед каждым запуском очередного пакета файл просто перезаписывается.
            _transferStore.WriteEnvelope(context.TmpJsonPath, transferEnvelope);

            context.ExportLog.Info(
                "Запуск Revit {0}. Моделей в пакете: {1}. Таймаут ожидания: {2}",
                batch.RevitMajor,
                batch.Models.Count,
                context.Settings.RevitBatchTimeoutMinutes > 0
                    ? $"{context.Settings.RevitBatchTimeoutMinutes} мин."
                    : "без ограничения");

            var ok = _revitLauncher.RunAndWait(
                batch.RevitMajor,
                taskPath,
                context.Paths.DirAdminData,
                iniPath: SettingsIniLocator.ResolveForChildProcess(),
                runId: context.RunId,
                timeoutMinutes: context.Settings.RevitBatchTimeoutMinutes);

            if (ok)
                return true;

            context.ExportLog.Warn(
                "Пакет Revit {0} завершился с ошибками. Проверьте txt-логи.",
                batch.RevitMajor);

            return false;
        }
        catch (Exception ex)
        {
            context.ExportLog.Error(
                "Не удалось обработать пакет Revit {0}:{1}{2}",
                batch.RevitMajor,
                Environment.NewLine,
                ex);
            return false;
        }
    }
}
