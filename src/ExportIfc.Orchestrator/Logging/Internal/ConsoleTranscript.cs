using System.Globalization;

using ExportIfc.IO;
using ExportIfc.Config;

namespace ExportIfc.Logging;

/// <summary>
/// Зеркало консольного вывода оркестратора в технический txt-файл.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Дублирует консольный вывод оркестратора в технический txt-лог.
/// 2. Буферизует ранние строки до появления итогового пути лога текущего запуска.
/// 3. Изолирует best-effort запись зеркала от основного сценария.
///
/// Контракты:
/// 1. До вызова <see cref="Initialize"/> строки накапливаются только в памяти.
/// 2. После инициализации накопленные строки сбрасываются в файл в исходном порядке.
/// 3. Ошибки записи зеркала не выбрасываются наружу и отключают дальнейшее дублирование.
/// 4. Класс защищает собственное состояние отдельной синхронизацией и не использует
///    <see cref="ConsoleSync.Sync"/> как lock для файлового канала.
/// </remarks>
internal static class ConsoleTranscript
{
    private static readonly object _sync = new();
    private static readonly List<string> _pendingLines = new();

    private static string? _fullPath;
    private static bool _isBroken;

    /// <summary>
    /// Привязывает зеркало консоли к txt-файлу текущего запуска
    /// и при необходимости сбрасывает накопленный стартовый буфер.
    /// </summary>
    /// <param name="fullPath">Полный путь к txt-файлу зеркала консоли.</param>
    public static void Initialize(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return;

        lock (_sync)
        {
            if (_isBroken)
                return;

            if (string.Equals(_fullPath, fullPath, StringComparison.OrdinalIgnoreCase))
                return;

            _fullPath = fullPath;

            if (_pendingLines.Count == 0)
                return;

            try
            {
                foreach (var pendingLine in _pendingLines)
                    TechnicalLogWriter.AppendLine(_fullPath, pendingLine);

                _pendingLines.Clear();
            }
            catch
            {
                // Зеркало консоли работает в best-effort режиме:
                // при ошибке файловый канал переводится в отключённое состояние
                // и основной прогон продолжается без него.
                _isBroken = true;
                _pendingLines.Clear();
            }
        }
    }

    /// <summary>
    /// Записывает в зеркало обычную строку компонентного лога.
    /// </summary>
    /// <param name="timestamp">Подготовленная дата и время строки.</param>
    /// <param name="level">Короткий код уровня сообщения.</param>
    /// <param name="component">Короткое имя компонента-источника.</param>
    /// <param name="text">Текст лог-сообщения без markup-оформления.</param>
    public static void WriteLogLine(string timestamp, string level, string component, string text)
        => WriteLine($"{timestamp} {level} [{component}] {text}");

    /// <summary>
    /// Записывает в зеркало строку-разделитель этапа.
    /// </summary>
    /// <param name="title">Заголовок этапа.</param>
    public static void WriteRuleLine(string title)
        => WriteLine($"{Now()} RULE {title}");

    /// <summary>
    /// Записывает в зеркало итоговую строку завершения запуска.
    /// </summary>
    /// <param name="isSuccess">
    /// <see langword="true"/>, если запуск завершён успешно;
    /// иначе <see langword="false"/>.
    /// </param>
    /// <param name="title">Короткий итоговый заголовок.</param>
    /// <param name="details">Дополнительная строка пояснения.</param>
    public static void WriteResultLine(bool isSuccess, string? title, string? details)
    {
        var status = isSuccess
            ? BatchRunStatuses.Ok
            : BatchRunStatuses.Failed;

        WriteLine($"{Now()} RESULT {status} | {title ?? string.Empty} | {details ?? string.Empty}");
    }

    /// <summary>
    /// Пишет одну plain-text строку в зеркало консоли или во временный буфер.
    /// </summary>
    /// <param name="line">Подготовленная строка без markup-оформления.</param>
    private static void WriteLine(string line)
    {
        lock (_sync)
        {
            if (_isBroken)
                return;

            if (string.IsNullOrWhiteSpace(_fullPath))
            {
                _pendingLines.Add(line);
                return;
            }

            try
            {
                TechnicalLogWriter.AppendLine(_fullPath, line);
            }
            catch
            {
                // При сбое запись в txt-зеркало отключается:
                // приоритет сохраняется за консольным прогоном.
                _isBroken = true;
            }
        }
    }

    /// <summary>
    /// Возвращает текущую дату и время в формате консольного техлога.
    /// </summary>
    /// <returns>Строка даты и времени в формате <see cref="ProjectFormats.DateTimeDisplay"/>.</returns>
    private static string Now()
        => DateTime.Now.ToString(ProjectFormats.DateTimeDisplay, CultureInfo.InvariantCulture);
}
