using ExportIfc.Config;
using ExportIfc.RevitAddin.Batch.Context;
using ExportIfc.RevitAddin.Logging;
using ExportIfc.Transfer;

namespace ExportIfc.RevitAddin.Batch.Input;

/// <summary>
/// Загружает входные данные пакетного запуска add-in.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Прочитать <see cref="ProjectFileNames.TmpJson"/> текущего пакета.
/// 2. Прочитать Task-файл текущего пакета как административное зеркало.
/// 3. Жёстко сверить txt и json, чтобы не запускать выгрузку на битом контракте.
/// 4. Подготовить рабочие данные для batch-исполнителя.
///
/// Контракты:
/// 1. При любой ошибке загрузки или сверки метод возвращает <see langword="false"/>
///    и фиксирует статус batch-запуска.
/// 2. Основным источником рабочих заданий остаётся <see cref="ProjectFileNames.TmpJson"/>.
/// 3. Task-файл используется как административная сверка согласованности пакета.
/// </remarks>
internal sealed class BatchRunInputLoader
{
    private readonly ITransferStore _transferStore;

    /// <summary>
    /// Создаёт загрузчик входных данных пакетного запуска.
    /// </summary>
    /// <param name="transferStore">Сервис чтения transfer-данных и Task-файла.</param>
    internal BatchRunInputLoader(ITransferStore transferStore)
    {
        _transferStore = transferStore ?? throw new ArgumentNullException(nameof(transferStore));
    }

    /// <summary>
    /// Пробует загрузить входные данные пакетного запуска.
    /// </summary>
    /// <param name="context">Контекст текущего batch-запуска.</param>
    /// <param name="input">Готовые входные данные или <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/>, если входные данные успешно загружены и сверены;
    /// иначе — <see langword="false"/>.
    /// </returns>
    public bool TryLoad(
        BatchRunContext context,
        out BatchRunInput? input)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        input = null;

        if (!File.Exists(context.TmpJsonPath))
        {
            FailLoadWithStatus(
                context,
                $"Не найден {ProjectFileNames.TmpJson}: '{context.TmpJsonPath}'.",
                $"Не найден {ProjectFileNames.TmpJson}.");

            return false;
        }

        if (!_transferStore.TryReadEnvelope(context.TmpJsonPath, out var envelope) || envelope is null)
        {
            FailLoadWithStatus(
                context,
                $"Не удалось разобрать {ProjectFileNames.TmpJson}: '{context.TmpJsonPath}'.",
                $"Ошибка разбора {ProjectFileNames.TmpJson}.");

            return false;
        }

        if (envelope.Items.Count == 0)
        {
            FailLoadWithStatus(
                context,
                $"Файл {ProjectFileNames.TmpJson} прочитан, но список Items пуст.",
                $"В {ProjectFileNames.TmpJson} нет заданий для обработки.");

            return false;
        }

        // tmp.json — общий transport-файл real-run. Перед обработкой пакета add-in
        // дополнительно подтверждает, что файл относится именно к текущему запуску
        // и к той major-версии Revit, в которой сейчас работает процесс.
        if (!string.Equals(envelope.RunId, context.RunId, StringComparison.Ordinal))
        {
            FailLoadWithStatus(
                context,
                $"Обнаружено расхождение RunId между {ProjectFileNames.TmpJson} и env-контрактом batch-запуска: tmp.json='{envelope.RunId}', env='{context.RunId}'.",
                $"Расхождение RunId между {ProjectFileNames.TmpJson} и batch-контекстом.");

            return false;
        }

        if (envelope.RevitMajor != context.RevitMajor)
        {
            FailLoadWithStatus(
                context,
                $"Обнаружено расхождение major-версии Revit между {ProjectFileNames.TmpJson} и env-контрактом batch-запуска: tmp.json={envelope.RevitMajor}, env={context.RevitMajor}.",
                $"Расхождение major-версии Revit между {ProjectFileNames.TmpJson} и batch-контекстом.");

            return false;
        }

        if (!File.Exists(context.TaskFilePath))
        {
            FailLoadWithStatus(
                context,
                $"Не найден файл списка моделей: '{context.TaskFilePath}'.",
                "Не найден файл списка моделей текущего пакета.");

            return false;
        }

        string[] taskModels;
        string? mismatch;

        try
        {
            // Task-файл остаётся человекочитаемым зеркалом пакета и отдельной
            // административной сверкой того, что orchestrator подготовил тот же
            // самый список моделей, который попал в JSON-пакет.
            taskModels = _transferStore.ReadTaskModels(context.TaskFilePath);
            mismatch = _transferStore.DescribeTaskModelMismatch(envelope, taskModels);
        }
        catch (Exception ex)
        {
            FailLoadWithStatus(
                context,
                $"Не удалось прочитать или сверить Task-файл: '{context.TaskFilePath}'. {ex.Message}",
                "Ошибка чтения или сверки Task-файла текущего пакета.");

            return false;
        }

        if (mismatch is not null)
        {
            FailLoadWithStatus(
                context,
                $"Обнаружено расхождение между {ProjectFileNames.TmpJson} и Task-файлом: {mismatch}",
                $"Расхождение между {ProjectFileNames.TmpJson} и Task-файлом текущего пакета.");

            return false;
        }

        input = new BatchRunInput(envelope, taskModels);
        return true;
    }

    /// <summary>
    /// Фиксирует ошибку загрузки входных данных в startup-логе и статусе запуска.
    /// </summary>
    /// <param name="context">Контекст текущего batch-запуска.</param>
    /// <param name="startupMessage">Подробное сообщение для startup-лога.</param>
    /// <param name="finalMessage">Короткое итоговое сообщение для файла статуса.</param>
    private static void FailLoadWithStatus(
        BatchRunContext context,
        string startupMessage,
        string finalMessage)
    {
        AddinLogs.WriteStartup(context.DirAdminData, startupMessage);
        AddinLogs.TryWriteRunStatus(context.DirAdminData, BatchRunStatuses.Failed, finalMessage);
    }
}
