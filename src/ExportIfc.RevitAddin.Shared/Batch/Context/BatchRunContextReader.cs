using ExportIfc.Config;
using ExportIfc.RevitAddin.Logging;
using ExportIfc.Settings;
using ExportIfc.Settings.Loading;

namespace ExportIfc.RevitAddin.Batch.Context;

/// <summary>
/// Читает и валидирует runtime-контекст текущего batch-запуска.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Считывает текущий снимок batch-окружения add-in.
/// 2. Проверяет обязательные runtime-параметры перед созданием <see cref="BatchRunContext"/>.
/// 3. Загружает обязательный <see cref="ProjectFileNames.SettingsIni"/>.
/// 4. Преобразует ошибки чтения контекста в управляемую остановку без выброса исключений наружу.
/// </remarks>
internal sealed class BatchRunContextReader
{
    /// <summary>
    /// Пробует прочитать контекст текущего batch-запуска.
    /// </summary>
    /// <param name="context">Подготовленный контекст пакетного запуска или <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/>, если контекст успешно прочитан;
    /// иначе <see langword="false"/>.
    /// </returns>
    public bool TryRead(out BatchRunContext? context)
    {
        context = null;

        var environment = BatchRunEnvironmentSnapshot.Read();

        // Без AdminData нельзя ни собрать пригодный к работе контекст,
        // ни зафиксировать технический статус текущего запуска.
        if (string.IsNullOrWhiteSpace(environment.DirAdminData))
            return false;

        if (!TryReadRequiredEnvironment(environment, out var runId, out var revitMajor))
            return false;

        if (!TryLoadSettings(environment.IniPath, out var settings, out var settingsError))
        {
            return FailReadWithStatus(
                environment.DirAdminData,
                settingsError,
                settingsError);
        }

        context = new BatchRunContext(
            environment.DirAdminData,
            environment.TaskFilePath,
            environment.IniPath,
            runId,
            revitMajor,
            ProjectFiles.TmpJson(environment.DirAdminData),
            settings.RevitExportView3dName,
            settings.EnableUnmappedExport);

        return true;
    }

    /// <summary>
    /// Проверяет обязательные значения окружения для текущего batch-запуска.
    /// </summary>
    /// <param name="environment">Снимок текущего batch-окружения.</param>
    /// <param name="runId">Провалидированный идентификатор запуска.</param>
    /// <param name="revitMajor">Провалидированная положительная major-версия Revit.</param>
    /// <returns>
    /// <see langword="true"/>, если все обязательные значения присутствуют и корректны;
    /// иначе <see langword="false"/>.
    /// </returns>
    private static bool TryReadRequiredEnvironment(
        BatchRunEnvironmentSnapshot environment,
        out string runId,
        out int revitMajor)
    {
        runId = string.Empty;
        revitMajor = 0;

        if (string.IsNullOrWhiteSpace(environment.TaskFilePath))
        {
            return FailMissingRequiredEnvironmentValue(
                environment.DirAdminData,
                EnvironmentVariableNames.TaskFile,
                "файл списка моделей текущего пакета");
        }

        if (string.IsNullOrWhiteSpace(environment.IniPath))
        {
            return FailMissingRequiredEnvironmentValue(
                environment.DirAdminData,
                EnvironmentVariableNames.SettingsIni,
                ProjectFileNames.SettingsIni);
        }

        if (string.IsNullOrWhiteSpace(environment.RunId))
        {
            return FailMissingRequiredEnvironmentValue(
                environment.DirAdminData,
                EnvironmentVariableNames.RunId,
                "идентификатор запуска");
        }

        if (!int.TryParse(environment.RevitMajor, out revitMajor) || revitMajor <= 0)
        {
            return FailInvalidEnvironmentValue(
                environment.DirAdminData,
                EnvironmentVariableNames.RevitMajor,
                environment.RevitMajor,
                "major-версия Revit текущего запуска");
        }

        runId = environment.RunId;
        return true;
    }

    /// <summary>
    /// Пытается загрузить настройки из ini-файла текущего запуска.
    /// </summary>
    /// <param name="iniPath">Путь к <see cref="ProjectFileNames.SettingsIni"/>.</param>
    /// <param name="settings">Загруженные настройки приложения.</param>
    /// <param name="errorMessage">Подробный текст ошибки загрузки.</param>
    /// <returns>
    /// <see langword="true"/>, если настройки успешно загружены;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Метод ожидает, что <paramref name="iniPath"/> уже провалидирован как обязательный путь.
    /// </remarks>
    private static bool TryLoadSettings(
        string iniPath,
        out AppSettings settings,
        out string errorMessage)
    {
        settings = default!;
        errorMessage = string.Empty;

        try
        {
            settings = AppSettingsLoader.Load(iniPath);

            if (settings is null)
            {
                errorMessage =
                    $"Не удалось получить настройки из {ProjectFileNames.SettingsIni} по пути '{iniPath}'.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage =
                $"Не удалось загрузить {ProjectFileNames.SettingsIni} по пути '{iniPath}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Фиксирует отсутствие обязательной переменной окружения.
    /// </summary>
    /// <param name="dirAdminData">Каталог admin-data текущего batch-запуска.</param>
    /// <param name="variableName">Имя отсутствующей переменной окружения.</param>
    /// <param name="logicalName">Пользовательское логическое имя обязательного runtime-ресурса.</param>
    /// <returns>Всегда возвращает <see langword="false"/>.</returns>
    private static bool FailMissingRequiredEnvironmentValue(
        string dirAdminData,
        string variableName,
        string logicalName)
    {
        return FailReadWithStatus(
            dirAdminData,
            $"Не задана переменная окружения {variableName}.",
            $"Не задан обязательный runtime-параметр: {logicalName}.");
    }

    /// <summary>
    /// Фиксирует некорректное значение обязательной переменной окружения.
    /// </summary>
    /// <param name="dirAdminData">Каталог admin-data текущего batch-запуска.</param>
    /// <param name="variableName">Имя переменной окружения с некорректным значением.</param>
    /// <param name="actualValue">Фактическое сырое значение из окружения.</param>
    /// <param name="logicalName">Пользовательское логическое имя обязательного runtime-параметра.</param>
    /// <returns>Всегда возвращает <see langword="false"/>.</returns>
    private static bool FailInvalidEnvironmentValue(
        string dirAdminData,
        string variableName,
        string actualValue,
        string logicalName)
    {
        return FailReadWithStatus(
            dirAdminData,
            string.Format("Некорректное значение переменной окружения {0}: '{1}'.", variableName, actualValue),
            string.Format("Некорректный обязательный параметр batch-запуска: {0}.", logicalName));
    }

    /// <summary>
    /// Пишет ошибку чтения контекста в startup-лог и файл статуса запуска.
    /// </summary>
    /// <param name="dirAdminData">Каталог admin-data текущего batch-запуска.</param>
    /// <param name="startupMessage">Подробное сообщение для startup-лога.</param>
    /// <param name="statusMessage">Короткое сообщение для файла статуса запуска.</param>
    /// <returns>Всегда возвращает <see langword="false"/>.</returns>
    private static bool FailReadWithStatus(
        string dirAdminData,
        string startupMessage,
        string statusMessage)
    {
        AddinLogs.BeginStartupSession(dirAdminData, startupMessage);
        AddinLogs.TryWriteRunStatus(dirAdminData, BatchRunStatuses.Failed, statusMessage);
        return false;
    }
}
