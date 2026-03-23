using ExportIfc.Config;
using ExportIfc.Transfer;

namespace ExportIfc.RevitAddin.Batch.Input;

/// <summary>
/// Входные данные batch-запуска после чтения
/// <see cref="ProjectFileNames.TmpJson"/> и файла списка моделей текущего пакета.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Хранит уже прочитанный transfer-envelope текущего пакета.
/// 2. Даёт batch-исполнителю рабочий снимок заданий из json.
/// 3. Сохраняет содержимое Task-файла для диагностики и административной сверки.
///
/// Контракты:
/// 1. Экземпляр содержит только уже загруженные входные данные и не выполняет повторного чтения файлов.
/// 2. <see cref="Items"/> фиксирует рабочий снимок заданий из <see cref="Envelope"/> на момент создания.
/// 3. <see cref="TaskModels"/> используется только для диагностики и сверки, а не как основной источник заданий.
/// </remarks>
internal sealed class BatchRunInput
{
    /// <summary>
    /// Создаёт набор входных данных batch-запуска.
    /// </summary>
    /// <param name="envelope">Содержимое <see cref="ProjectFileNames.TmpJson"/> текущего пакета.</param>
    /// <param name="taskModels">Пути моделей из Task-файла текущего пакета.</param>
    public BatchRunInput(
        TransferEnvelope envelope,
        string[] taskModels)
    {
        Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
        TaskModels = taskModels?.ToArray() ?? throw new ArgumentNullException(nameof(taskModels));

        // Рабочий список фиксируем отдельно, чтобы batch-исполнитель
        // не зависел от дальнейших изменений исходной коллекции envelope.
        Items = Envelope.Items.ToArray();
    }

    /// <summary>
    /// Содержимое <see cref="ProjectFileNames.TmpJson"/>.
    /// </summary>
    public TransferEnvelope Envelope { get; }

    /// <summary>
    /// Задания текущего batch-пакета в рабочем порядке.
    /// </summary>
    public IReadOnlyList<TransferItem> Items { get; }

    /// <summary>
    /// Пути моделей из Task-файла текущего пакета.
    /// Используются только для диагностики и административной сверки.
    /// </summary>
    public string[] TaskModels { get; }
}