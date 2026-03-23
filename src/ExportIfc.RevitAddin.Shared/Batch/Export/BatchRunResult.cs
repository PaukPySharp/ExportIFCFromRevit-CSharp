using ExportIfc.Config;

namespace ExportIfc.RevitAddin.Batch.Export;

/// <summary>
/// Итог пакетной обработки внутри add-in.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Зафиксировать агрегированный результат batch-запуска.
/// 2. Дать orchestration-слою готовый статус и человекочитаемое итоговое сообщение.
///
/// Контракты:
/// 1. <see cref="ProcessedCount"/> отражает количество моделей, реально переданных в обработку.
/// 2. <see cref="FinalStatus"/> и <see cref="FinalMessage"/> вычисляются только из текущего состояния результата.
/// </remarks>
internal sealed class BatchRunResult
{
    /// <summary>
    /// Создаёт итог пакетной обработки.
    /// </summary>
    /// <param name="hasErrors">Признак наличия ошибок по хотя бы одной модели пакета.</param>
    /// <param name="processedCount">Количество моделей, реально переданных в обработку.</param>
    public BatchRunResult(bool hasErrors, int processedCount)
    {
        HasErrors = hasErrors;
        ProcessedCount = processedCount;
    }

    /// <summary>
    /// Получает признак наличия ошибок в рамках текущего пакета.
    /// </summary>
    public bool HasErrors { get; }

    /// <summary>
    /// Получает количество моделей, реально переданных в обработку.
    /// </summary>
    public int ProcessedCount { get; }

    /// <summary>
    /// Получает финальный статус пакетного выполнения.
    /// </summary>
    public string FinalStatus =>
        HasErrors
            ? BatchRunStatuses.Partial
            : BatchRunStatuses.Ok;

    /// <summary>
    /// Получает финальное текстовое сообщение пакетного выполнения.
    /// </summary>
    public string FinalMessage =>
        HasErrors
            ? $"Пакет обработан частично. Моделей передано в обработку: {ProcessedCount}. " +
                "Были ошибки подготовки модели, поиска вида, экспорта, проверки результата или закрытия документа."
            : $"Пакет обработан успешно. Моделей передано в обработку: {ProcessedCount}.";
}