using ExportIfc.RevitAddin.Batch.Context;
using ExportIfc.RevitAddin.Logging;

namespace ExportIfc.RevitAddin.Revit;

internal static partial class RevitDocuments
{
    /// <summary>
    /// Возвращает строковое представление статуса чтения BasicFileInfo для технического лога.
    /// </summary>
    /// <param name="probe">Результат чтения BasicFileInfo.</param>
    /// <returns>Строка <c>Ok</c> или <c>Fail</c>.</returns>
    private static string GetProbeStatusLabel(BasicFileInfoProbeResult probe)
    {
        return probe.ExtractSucceeded
            ? "Ok"
            : "Fail";
    }

    /// <summary>
    /// Возвращает строковое представление workshared-статуса модели.
    /// </summary>
    /// <param name="probe">Результат чтения BasicFileInfo.</param>
    /// <returns>
    /// <c>True</c>, <c>False</c> или <c>Unknown</c>, если метаданные не прочитались.
    /// </returns>
    private static string GetIsWorksharedLabel(BasicFileInfoProbeResult probe)
    {
        if (!probe.ExtractSucceeded)
            return "Unknown";

        return probe.IsWorkshared
            ? "True"
            : "False";
    }

    /// <summary>
    /// Возвращает метку результата попытки открытия для технического лога.
    /// </summary>
    /// <param name="error">Исключение попытки.</param>
    /// <param name="returnedNull">Признак null-результата Revit API.</param>
    /// <returns>Короткая строковая метка результата.</returns>
    private static string BuildAttemptResultLabel(Exception? error, bool returnedNull)
    {
        if (error is not null)
            return "Exception";

        return returnedNull
            ? "NullDocument"
            : "Unknown";
    }

    /// <summary>
    /// Пишет сообщение в startup-лог add-in, если текущий batch-сеанс содержит каталог admin-data.
    /// </summary>
    /// <param name="text">Текст технического сообщения.</param>
    private static void WriteStartup(string text)
    {
        try
        {
            var dirAdminData = BatchRunEnvironmentSnapshot.Read().DirAdminData;
            if (string.IsNullOrWhiteSpace(dirAdminData))
                return;

            AddinLogs.WriteStartup(dirAdminData, text);
        }
        catch
        {
            // Ошибки технического логирования не должны мешать открытию модели.
        }
    }

    /// <summary>
    /// Преобразует режим открытия в короткое значение для технического лога.
    /// </summary>
    /// <param name="openMode">Режим открытия.</param>
    /// <returns>Строковое значение режима.</returns>
    private static string ToLogValue(BatchOpenMode openMode)
    {
        return openMode == BatchOpenMode.Detached
            ? "Detached"
            : "Direct";
    }

    /// <summary>
    /// Возвращает однострочное представление исключения для технического лога.
    /// </summary>
    /// <param name="ex">Исключение.</param>
    /// <returns>Короткая однострочная сводка исключения.</returns>
    private static string FormatExceptionSummary(Exception ex)
    {
        var message = ex.Message
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        if (string.IsNullOrWhiteSpace(message))
            return ex.GetType().FullName ?? ex.GetType().Name;

        return $"{ex.GetType().FullName}: {message}";
    }
}
