using ExportIfc.Config;
using ExportIfc.IO;
using ExportIfc.RevitAddin.Batch.Context;

namespace ExportIfc.RevitAddin.Logging;

/// <summary>
/// Фасад записи технических логов add-in.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует запись служебных логов batch-addin внутри процесса Revit.
/// 2. Разделяет daily-лог, startup-лог, fatal-лог и журнал статусов запуска.
/// 3. Скрывает форматирование технических записей от runtime-логики.
///
/// Контракты:
/// 1. Публичные методы работают по best-effort-сценарию и не должны валить batch-процесс
///    из-за ошибок записи логов.
/// 2. Startup-сессия открывается не более одного раза за процесс add-in.
/// 3. Данные запуска для заголовков и статусов берутся из <see cref="BatchRunEnvironmentSnapshot"/>.
/// </remarks>
internal static class AddinLogs
{
    // Флаг защищает startup-лог от повторной записи стартовой шапки в рамках одного процесса Revit.
    private static bool _startupSessionOpened;

    /// <summary>
    /// Пишет пользовательскую запись в ежедневный txt-лог.
    /// </summary>
    /// <param name="dirAdminData">Каталог <see cref="ProjectDirectoryNames.AdminData"/>.</param>
    /// <param name="baseName">Базовое имя лог-файла.</param>
    /// <param name="text">Текст записи.</param>
    public static void WriteDaily(string dirAdminData, string baseName, string text)
    {
        try
        {
            var dirLogs = ProjectDirectories.Logs(dirAdminData);
            TextLogs.WriteLines(dirLogs, baseName, [text], separator: string.Empty);
        }
        catch
        {
            // Игнорируем ошибки записи ежедневного лога.
        }
    }

    /// <summary>
    /// Открывает блок технического лога текущего запуска add-in.
    /// </summary>
    /// <param name="dirAdminData">Каталог <see cref="ProjectDirectoryNames.AdminData"/>.</param>
    /// <param name="details">Начальные строки блока.</param>
    /// <remarks>
    /// Стартовая шапка пишется один раз за процесс add-in.
    /// При ошибке записи сессия не считается открытой, чтобы следующий вызов мог повторить попытку.
    /// </remarks>
    public static void BeginStartupSession(string dirAdminData, params string[] details)
    {
        if (_startupSessionOpened)
            return;

        try
        {
            TechnicalLogWriter.AppendBlock(
                ProjectFiles.AddinStartupLog(dirAdminData),
                BuildHeaderLine(AddinLogSchema.StartupBlockType),
                details,
                useBullets: true);

            _startupSessionOpened = true;
        }
        catch
        {
            // Игнорируем ошибки открытия startup-лога.
        }
    }

    /// <summary>
    /// Пишет служебное сообщение в лог текущего запуска add-in.
    /// </summary>
    /// <param name="dirAdminData">Каталог <see cref="ProjectDirectoryNames.AdminData"/>.</param>
    /// <param name="text">Текст записи.</param>
    public static void WriteStartup(string dirAdminData, string text)
    {
        try
        {
            if (!_startupSessionOpened)
            {
                BeginStartupSession(
                    dirAdminData,
                    "Сеанс add-in запущен без стартовой шапки.");
            }

            var timestamp = DateTime.Now.ToString(ProjectFormats.TimeOnly);

            TechnicalLogWriter.AppendLine(
                ProjectFiles.AddinStartupLog(dirAdminData),
                $"  - [{timestamp}] {text.Trim()}");
        }
        catch
        {
            // Игнорируем ошибки записи технического лога.
        }
    }

    /// <summary>
    /// Пишет фатальное исключение add-in в отдельный технический лог.
    /// </summary>
    /// <param name="ex">Исключение.</param>
    public static void TryWriteFatal(Exception ex)
    {
        try
        {
            var environment = BatchRunEnvironmentSnapshot.Read();

            if (string.IsNullOrWhiteSpace(environment.DirAdminData))
                return;

            TechnicalLogWriter.AppendBlock(
                ProjectFiles.AddinFatalLog(environment.DirAdminData),
                BuildHeaderLine(AddinLogSchema.FatalBlockType),
                SplitLines(ex.ToString()),
                useBullets: false);
        }
        catch
        {
            // Игнорируем ошибки записи фатального лога.
        }
    }

    /// <summary>
    /// Пишет результат выполнения add-in в журнал статусов.
    /// </summary>
    /// <param name="dirAdminData">Каталог <see cref="ProjectDirectoryNames.AdminData"/>.</param>
    /// <param name="status">Короткий статус из <see cref="BatchRunStatuses"/>.</param>
    /// <param name="message">Пояснение к статусу.</param>
    public static void TryWriteRunStatus(
        string dirAdminData,
        string status,
        string? message = null)
    {
        try
        {
            var environment = BatchRunEnvironmentSnapshot.Read();
            var runId = FormatRunId(environment.RunId);
            var revitMajor = FormatRevitMajor(environment.RevitMajor);

            var body = new List<string>
            {
                AddinLogSchema.RunIdPrefix + runId,
                AddinLogSchema.RevitMajorPrefix + revitMajor,
                AddinLogSchema.TaskFilePrefix + environment.TaskFileName,
                AddinLogSchema.StatusPrefix + status
            };

            var trimmedMessage = message?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedMessage))
                body.Add(AddinLogSchema.MessagePrefix + trimmedMessage);

            TechnicalLogWriter.AppendBlock(
                ProjectFiles.AddinStatusFile(dirAdminData),
                BuildHeaderLine(AddinLogSchema.StatusBlockType),
                body,
                useBullets: false);
        }
        catch
        {
            // Игнорируем ошибки записи файла статуса.
        }
    }

    /// <summary>
    /// Пишет результат выполнения add-in, беря каталог
    /// <see cref="ProjectDirectoryNames.AdminData"/> из текущего batch-окружения.
    /// </summary>
    /// <param name="status">Короткий статус из <see cref="BatchRunStatuses"/>.</param>
    /// <param name="message">Пояснение к статусу.</param>
    public static void TryWriteRunStatusFromEnv(
        string status,
        string? message = null)
    {
        try
        {
            var environment = BatchRunEnvironmentSnapshot.Read();

            if (string.IsNullOrWhiteSpace(environment.DirAdminData))
                return;

            TryWriteRunStatus(environment.DirAdminData, status, message);
        }
        catch
        {
            // Игнорируем ошибки записи файла статуса.
        }
    }

    /// <summary>
    /// Строит заголовок технического блока лога из текущего batch-окружения.
    /// </summary>
    /// <param name="blockType">Тип логического блока из <see cref="AddinLogSchema"/>.</param>
    /// <returns>Строка заголовка для записи в технический лог.</returns>
    private static string BuildHeaderLine(string blockType)
    {
        var environment = BatchRunEnvironmentSnapshot.Read();
        var timestamp = DateTime.Now.ToString(ProjectFormats.DateTimeDisplay);
        var runId = FormatRunId(environment.RunId);
        var revitMajor = FormatRevitMajor(environment.RevitMajor);

        return
            $"[{timestamp}] [{blockType}] " +
            $"{AddinLogSchema.HeaderRunIdLabel}={runId} " +
            $"{AddinLogSchema.HeaderRevitLabel}={revitMajor} " +
            $"{AddinLogSchema.HeaderTaskLabel}={environment.TaskFileName}";
    }

    /// <summary>
    /// Возвращает текстовый маркер RunId для технических логов.
    /// </summary>
    /// <param name="runId">Значение RunId из окружения текущего процесса.</param>
    /// <returns>Фактический RunId или служебную заглушку.</returns>
    private static string FormatRunId(string? runId)
    {
        var value = runId?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? "<no-run-id>"
            : value!;
    }

    /// <summary>
    /// Возвращает текстовый маркер major-версии Revit для технических логов.
    /// </summary>
    /// <param name="revitMajor">Значение RevitMajor из окружения текущего процесса.</param>
    /// <returns>Фактическую major-версию или служебную заглушку.</returns>
    private static string FormatRevitMajor(string? revitMajor)
    {
        var value = revitMajor?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? "<no-revit>"
            : value!;
    }

    /// <summary>
    /// Разбивает многострочный текст на отдельные строки в нормализованном формате переводов строк.
    /// </summary>
    /// <param name="text">Исходный текст.</param>
    /// <returns>Последовательность строк без различий между CRLF, CR и LF.</returns>
    private static IEnumerable<string> SplitLines(string text)
    {
        return text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');
    }
}
