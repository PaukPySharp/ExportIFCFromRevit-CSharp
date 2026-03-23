using System.Diagnostics;

using ExportIfc.Config;

namespace ExportIfc.Revit;

/// <summary>
/// Фабрика параметров запуска внешнего процесса Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует создание <see cref="ProcessStartInfo"/> для batch-запуска Revit.
/// 2. Настраивает autorun-контракт между оркестратором и add-in через переменные окружения.
/// 3. Убирает дублирование параметров запуска из orchestration-логики.
///
/// Контракты:
/// 1. Внешний процесс запускается через локальный
///    <see cref="RevitConstants.ExecutableFileName"/>.
/// 2. Add-in получает все служебные параметры через переменные окружения.
/// 3. Аргументы запуска Revit берутся централизованно из <see cref="RevitConstants"/>.
/// </remarks>
internal static class RevitProcessStartInfoFactory
{
    /// <summary>
    /// Создаёт <see cref="ProcessStartInfo"/> для пакетного запуска Revit.
    /// </summary>
    /// <param name="exePath">Полный путь к <see cref="RevitConstants.ExecutableFileName"/>.</param>
    /// <param name="revitMajor">Целевая major-версия Revit.</param>
    /// <param name="taskFilePath">Путь к <see cref="ProjectFileNames.TaskFileDisplayName"/> текущего пакета.</param>
    /// <param name="dirAdminData">Каталог admin-data текущего запуска.</param>
    /// <param name="iniPath">Путь к <see cref="ProjectFileNames.SettingsIni"/> текущего запуска.</param>
    /// <param name="runId">Идентификатор текущего запуска оркестратора.</param>
    /// <returns>Готовый набор параметров запуска процесса Revit.</returns>
    /// <remarks>
    /// Метод формирует environment-контракт, который затем читает add-in
    /// внутри autorun-сценария.
    /// </remarks>
    public static ProcessStartInfo Create(
        string exePath,
        int revitMajor,
        string taskFilePath,
        string dirAdminData,
        string iniPath,
        string runId)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = RevitConstants.NoSplashArguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Все служебные параметры запуска передаются через environment,
        // чтобы внешний Revit-процесс и add-in получили единый runtime-контракт.
        startInfo.Environment[EnvironmentVariableNames.Autorun] = EnvironmentVariableValues.AutorunEnabled;
        startInfo.Environment[EnvironmentVariableNames.AdminData] = dirAdminData;
        startInfo.Environment[EnvironmentVariableNames.TaskFile] = taskFilePath;
        startInfo.Environment[EnvironmentVariableNames.RevitMajor] = revitMajor.ToString();
        startInfo.Environment[EnvironmentVariableNames.SettingsIni] = iniPath;
        startInfo.Environment[EnvironmentVariableNames.RunId] = runId;

#if DEBUG
        // Отладочный рубильник ранней паузы внутри Revit add-in.
        // false — обычный debug-запуск без ожидания attach.
        // true  — add-in ждёт подключения debugger в DebugAttachGate.WaitIfRequested().
        var enableDebugWaitAttach = false;

        if (enableDebugWaitAttach)
        {
            startInfo.Environment[EnvironmentVariableNames.DebugWaitAttach] =
                EnvironmentVariableValues.DebugWaitAttachEnabled;
        }
#endif

        return startInfo;
    }
}
