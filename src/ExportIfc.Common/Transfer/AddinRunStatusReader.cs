using ExportIfc.Config;

namespace ExportIfc.Transfer;

/// <summary>
/// Чтение итогового статуса batch-выполнения из журнала add-in.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Ищет в журнале блоки, относящиеся к конкретной паре <c>RunId</c> и <c>TaskFile</c>.
/// 2. Извлекает последнее найденное значение <c>Status=...</c> для нужного batch-пакета.
/// 3. Позволяет оркестратору понять, чем завершился конкретный batch-запуск add-in.
///
/// Контракты:
/// 1. Метод работает в режиме best effort и не выбрасывает исключения наружу.
/// 2. Если файл отсутствует, недоступен или нужный статус не найден,
///    возвращается <see langword="null"/>.
/// 3. Поиск выполняется без учёта регистра.
/// 4. Если для одной и той же пары <c>RunId</c> и <c>TaskFile</c> в файле встретилось
///    несколько строк <c>Status=...</c>, возвращается последняя из них.
/// </remarks>
public static class AddinRunStatusReader
{
    /// <summary>
    /// Пробует прочитать итоговый статус add-in для заданных RunId и Task-файла.
    /// </summary>
    /// <param name="path">Путь к файлу журнала статусов.</param>
    /// <param name="runId">Идентификатор текущего запуска оркестратора.</param>
    /// <param name="taskFileName">Имя Task-файла текущего batch-пакета.</param>
    /// <returns>
    /// Итоговый статус для указанной пары RunId/TaskFile либо <see langword="null"/>,
    /// если статус не найден или файл недоступен.
    /// </returns>
    /// <remarks>
    /// Файл читается последовательно сверху вниз.
    /// Reader запоминает текущие значения <c>RunId</c> и <c>TaskFile</c>
    /// и обновляет результат только для подходящей пары.
    /// Один orchestrator-run может содержать несколько batch-пакетов
    /// с одним и тем же <c>RunId</c>.
    /// </remarks>
    public static string? TryReadStatus(
        string path,
        string runId,
        string taskFileName)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            string? currentRunId = null;
            string? currentTaskFile = null;
            string? matchedStatus = null;

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (TryReadValueAfterPrefix(line, AddinLogSchema.RunIdPrefix, out var parsedRunId))
                {
                    currentRunId = parsedRunId;
                    currentTaskFile = null;
                    continue;
                }

                if (TryReadValueAfterPrefix(line, AddinLogSchema.TaskFilePrefix, out var parsedTaskFile))
                {
                    currentTaskFile = parsedTaskFile;
                    continue;
                }

                if (TryReadValueAfterPrefix(line, AddinLogSchema.StatusPrefix, out var parsedStatus)
                    && string.Equals(currentRunId, runId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(currentTaskFile, taskFileName, StringComparison.OrdinalIgnoreCase))
                {
                    matchedStatus = parsedStatus;
                }
            }

            return matchedStatus;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Пробует извлечь значение после заданного строкового префикса.
    /// </summary>
    /// <param name="line">Строка журнала.</param>
    /// <param name="prefix">Ожидаемый префикс.</param>
    /// <param name="value">Извлечённое значение без префикса и внешних пробелов.</param>
    /// <returns>
    /// <see langword="true"/>, если строка начинается с указанного префикса;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Метод не интерпретирует семантику значения.
    /// Он только отделяет полезную часть строки от служебного префикса журнала.
    /// </remarks>
    private static bool TryReadValueAfterPrefix(
        string line,
        string prefix,
        out string value)
    {
        if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = line.Substring(prefix.Length).Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }
}
