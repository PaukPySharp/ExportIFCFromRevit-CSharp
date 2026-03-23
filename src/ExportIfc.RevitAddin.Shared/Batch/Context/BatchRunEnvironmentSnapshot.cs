using ExportIfc.Config;

namespace ExportIfc.RevitAddin.Batch.Context;

/// <summary>
/// Снимок параметров текущего batch-запуска add-in из переменных окружения.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует чтение runtime-параметров add-in из окружения процесса Revit.
/// 2. Даёт runtime-слою, reader-ам и логированию единый снимок данных текущего запуска.
///
/// Контракты:
/// 1. Экземпляр содержит нормализованные строковые значения текущего запуска,
///    используемые как вход для дальнейшей валидации и логирования.
/// 2. Отсутствующие или пустые переменные окружения нормализуются в предсказуемые значения.
/// 3. <see cref="TaskFileName"/> хранит имя файла без исходного пути
///    или служебную заглушку, если путь к task-файлу отсутствует.
/// </remarks>
internal sealed class BatchRunEnvironmentSnapshot
{
    /// <summary>
    /// Создаёт снимок параметров текущего batch-запуска.
    /// </summary>
    /// <param name="dirAdminData">Каталог <see cref="ProjectDirectoryNames.AdminData"/>.</param>
    /// <param name="taskFilePath">Путь к файлу списка моделей текущего пакета.</param>
    /// <param name="iniPath">Путь к <see cref="ProjectFileNames.SettingsIni"/> из окружения текущего запуска.</param>
    /// <param name="runId">Идентификатор текущего batch-запуска.</param>
    /// <param name="revitMajor">Целевая major-версия Revit текущего запуска.</param>
    /// <param name="taskFileName">Имя файла списка моделей текущего пакета без пути.</param>
    private BatchRunEnvironmentSnapshot(
        string dirAdminData,
        string taskFilePath,
        string iniPath,
        string runId,
        string revitMajor,
        string taskFileName)
    {
        DirAdminData = dirAdminData;
        TaskFilePath = taskFilePath;
        IniPath = iniPath;
        RunId = runId;
        RevitMajor = revitMajor;
        TaskFileName = taskFileName;
    }

    /// <summary>
    /// Каталог <see cref="ProjectDirectoryNames.AdminData"/> текущего запуска.
    /// </summary>
    public string DirAdminData { get; }

    /// <summary>
    /// Путь к файлу списка моделей текущего пакета.
    /// </summary>
    public string TaskFilePath { get; }

    /// <summary>
    /// Путь к <see cref="ProjectFileNames.SettingsIni"/> из окружения текущего запуска.
    /// </summary>
    public string IniPath { get; }

    /// <summary>
    /// Идентификатор текущего batch-запуска.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Целевая major-версия Revit текущего запуска.
    /// </summary>
    public string RevitMajor { get; }

    /// <summary>
    /// Имя файла списка моделей текущего пакета без пути
    /// или служебная заглушка, если путь к task-файлу отсутствует.
    /// </summary>
    public string TaskFileName { get; }

    /// <summary>
    /// Читает параметры batch-запуска add-in из переменных окружения.
    /// </summary>
    /// <returns>Готовый снимок параметров текущего batch-запуска.</returns>
    public static BatchRunEnvironmentSnapshot Read()
    {
        var taskFilePath = ReadEnv(EnvironmentVariableNames.TaskFile, string.Empty);
        var taskFileName = Path.GetFileName(taskFilePath);

        if (string.IsNullOrWhiteSpace(taskFileName))
            taskFileName = "<no-task-file>";

        return new BatchRunEnvironmentSnapshot(
            ReadEnv(EnvironmentVariableNames.AdminData, string.Empty),
            taskFilePath,
            ReadEnv(EnvironmentVariableNames.SettingsIni, string.Empty),
            ReadEnv(EnvironmentVariableNames.RunId, string.Empty),
            ReadEnv(EnvironmentVariableNames.RevitMajor, string.Empty),
            taskFileName);
    }

    /// <summary>
    /// Читает значение переменной окружения с нормализацией пустых значений.
    /// </summary>
    /// <param name="variableName">Имя переменной окружения.</param>
    /// <param name="fallback">Значение по умолчанию для пустого или отсутствующего env.</param>
    /// <returns>Нормализованное значение переменной или fallback.</returns>
    private static string ReadEnv(string variableName, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variableName);

        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }
}
