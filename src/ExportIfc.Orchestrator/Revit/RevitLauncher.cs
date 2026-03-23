using System;
using System.Diagnostics;

using ExportIfc.Config;
using ExportIfc.Logging;
using ExportIfc.Transfer;

namespace ExportIfc.Revit;

/// <summary>
/// Реализация запуска пакетного процесса Revit через локальный исполняемый файл.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Находит исполняемый файл нужной версии Revit.
/// 2. Подготавливает параметры запуска для autorun-сценария add-in.
/// 3. Дожидается завершения процесса Revit.
/// 4. Проверяет итоговый статус пакетной обработки через журнал add-in.
///
/// Контракты:
/// 1. Метод запуска ориентируется на конкретную major-версию Revit.
/// 2. Успешным считается только такой запуск, при котором add-in записал статус <see cref="BatchRunStatuses.Ok"/>.
/// 3. При превышении таймаута процесс Revit принудительно останавливается.
/// 4. Если пакет уже завершён, но Revit завис на этапе закрытия, launcher даёт короткое окно на штатный выход,
///    а затем останавливает процесс принудительно.
/// </remarks>
internal sealed class RevitLauncher : IRevitLauncher
{
    // Тайминги внутреннего алгоритма ожидания и завершения процесса Revit.
    private const int _processPollIntervalMilliseconds = 500;
    private static readonly TimeSpan _closePhaseTimeout =
        TimeSpan.FromSeconds(BatchRunThresholds.GracefulCloseWindowSeconds)
            .Add(TimeSpan.FromMilliseconds(_processPollIntervalMilliseconds));

    private readonly ConsoleLogger _launcherLog = Log.For(LogComponents.Launcher);
    private readonly IRevitExeLocator _revitExeLocator;

    /// <summary>
    /// Создаёт сервис запуска Revit с явно переданным поисковиком
    /// <see cref="RevitConstants.ExecutableFileName"/>.
    /// </summary>
    /// <param name="revitExeLocator">Сервис поиска локального <see cref="RevitConstants.ExecutableFileName"/>.</param>
    internal RevitLauncher(IRevitExeLocator revitExeLocator)
    {
        ArgumentNullException.ThrowIfNull(revitExeLocator);
        _revitExeLocator = revitExeLocator;
    }

    /// <inheritdoc />
    public bool RunAndWait(
        int revitMajor,
        string taskFilePath,
        string dirAdminData,
        string iniPath,
        string runId,
        int timeoutMinutes)
    {
        var exePath = _revitExeLocator.TryFind(revitMajor);
        if (exePath is null)
        {
            _launcherLog.Error("Не удалось найти Revit {0}.", revitMajor);
            return false;
        }

        var startInfo = RevitProcessStartInfoFactory.Create(
            exePath,
            revitMajor,
            taskFilePath,
            dirAdminData,
            iniPath,
            runId);

        using var process = TryStartProcess(startInfo, revitMajor);
        if (process is null)
            return false;

        var statusFilePath = ProjectFiles.AddinStatusFile(dirAdminData);
        var taskFileName = Path.GetFileName(taskFilePath);

        var waitResult = WaitForExit(
            process,
            revitMajor,
            timeoutMinutes,
            statusFilePath,
            runId,
            taskFileName,
            out var addinStatus);

        if (waitResult == ProcessWaitResult.TimedOut)
            return false;

        if (addinStatus is null)
            addinStatus = AddinRunStatusReader.TryReadStatus(statusFilePath, runId, taskFileName);

        if (waitResult == ProcessWaitResult.KilledAfterFinalStatus)
        {
            var statusAfterKill = string.IsNullOrWhiteSpace(addinStatus)
                ? "<не найден>"
                : addinStatus;

            if (string.Equals(addinStatus, BatchRunStatuses.Ok, StringComparison.OrdinalIgnoreCase))
            {
                _launcherLog.Warn(
                    "Revit {0} не закрылся штатно после успешной выгрузки. Процесс остановлен принудительно. RunId={1}, TaskFile={2}, Status='{3}'.",
                    revitMajor,
                    runId,
                    taskFileName,
                    statusAfterKill);

                return true;
            }

            _launcherLog.Warn(
                "Revit {0} не закрылся штатно после завершения batch-пакета. Процесс остановлен принудительно. RunId={1}, TaskFile={2}, Status='{3}'.",
                revitMajor,
                runId,
                taskFileName,
                statusAfterKill);

            return false;
        }

        if (process.ExitCode != 0)
        {
            _launcherLog.Error(
                "Revit {0} завершился с кодом {1}.",
                revitMajor,
                process.ExitCode);

            return false;
        }

        if (string.Equals(addinStatus, BatchRunStatuses.Ok, StringComparison.OrdinalIgnoreCase))
            return true;

        var statusForLog = string.IsNullOrWhiteSpace(addinStatus)
            ? "<не найден>"
            : addinStatus;

        _launcherLog.Warn(
            "Revit {0} завершился, но add-in не подтвердил успешную выгрузку для RunId={1}, TaskFile={2}. Status='{3}'. Файл: '{4}'",
            revitMajor,
            runId,
            taskFileName,
            statusForLog,
            statusFilePath);

        return false;
    }

    /// <summary>
    /// Пытается запустить внешний процесс Revit.
    /// </summary>
    /// <param name="startInfo">Подготовленные параметры запуска.</param>
    /// <param name="revitMajor">Major-версия запускаемого процесса.</param>
    /// <returns>Запущенный процесс или <see langword="null"/>.</returns>
    private Process? TryStartProcess(ProcessStartInfo startInfo, int revitMajor)
    {
        try
        {
            var process = Process.Start(startInfo);
            if (process is not null)
                return process;

            _launcherLog.Error("Не удалось запустить процесс Revit {0}.", revitMajor);
            return null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _launcherLog.Error(
                "Не удалось запустить процесс Revit {0}: {1}",
                revitMajor,
                ex.Message);

            return null;
        }
    }

    /// <summary>
    /// Ожидает завершения процесса Revit с учётом общего таймаута и отдельной фазы штатного закрытия.
    /// </summary>
    /// <param name="process">Запущенный процесс Revit.</param>
    /// <param name="revitMajor">Major-версия ожидаемого процесса.</param>
    /// <param name="timeoutMinutes">Общий таймаут batch-запуска в минутах. 0 или меньше означает ожидание без ограничения.</param>
    /// <param name="statusFilePath">Путь к файлу статусов add-in.</param>
    /// <param name="runId">Идентификатор текущего запуска оркестратора.</param>
    /// <param name="taskFileName">Имя Task-файла текущего batch-пакета.</param>
    /// <param name="addinStatus">Последний прочитанный итоговый статус add-in.</param>
    /// <returns>Результат ожидания процесса.</returns>
    private ProcessWaitResult WaitForExit(
        Process process,
        int revitMajor,
        int timeoutMinutes,
        string statusFilePath,
        string runId,
        string taskFileName,
        out string? addinStatus)
    {
        addinStatus = null;

        var startedAtUtc = DateTime.UtcNow;
        DateTime? finalStatusDetectedAtUtc = null;

        while (true)
        {
            if (process.WaitForExit(_processPollIntervalMilliseconds))
                return ProcessWaitResult.Exited;

            // После появления итогового статуса add-in launcher переходит
            // в отдельную короткую фазу ожидания штатного закрытия Revit.
            if (addinStatus is null)
            {
                var detectedStatus = AddinRunStatusReader.TryReadStatus(statusFilePath, runId, taskFileName);
                if (IsFinalAddinStatus(detectedStatus))
                {
                    addinStatus = detectedStatus;
                    finalStatusDetectedAtUtc = DateTime.UtcNow;
                }
            }

            if (finalStatusDetectedAtUtc.HasValue
                && DateTime.UtcNow - finalStatusDetectedAtUtc.Value >= _closePhaseTimeout)
            {
                _launcherLog.Warn(
                    "Revit {0} не завершился в течение {1} сек. после появления итогового статуса add-in. Процесс будет принудительно остановлен.",
                    revitMajor,
                    BatchRunThresholds.GracefulCloseWindowSeconds);

                if (TryKillProcessTree(process, revitMajor))
                    return ProcessWaitResult.KilledAfterFinalStatus;

                return ProcessWaitResult.TimedOut;
            }

            if (timeoutMinutes > 0
                && DateTime.UtcNow - startedAtUtc >= TimeSpan.FromMinutes(timeoutMinutes))
            {
                _launcherLog.Error(
                    "Revit {0} превысил таймаут ожидания ({1} мин.) и будет принудительно остановлен.",
                    revitMajor,
                    timeoutMinutes);

                TryKillProcessTree(process, revitMajor);
                return ProcessWaitResult.TimedOut;
            }
        }
    }

    /// <summary>
    /// Определяет, является ли статус add-in итоговым для batch-пакета.
    /// </summary>
    /// <param name="status">Статус из журнала add-in.</param>
    /// <returns>
    /// <see langword="true"/>, если статус означает завершённый результат;
    /// иначе <see langword="false"/>.
    /// </returns>
    private static bool IsFinalAddinStatus(string? status)
    {
        return string.Equals(status, BatchRunStatuses.Ok, StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, BatchRunStatuses.Partial, StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, BatchRunStatuses.Failed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Пытается принудительно завершить зависший процесс Revit.
    /// </summary>
    /// <param name="process">Запущенный процесс Revit.</param>
    /// <param name="revitMajor">Major-версия ожидаемого процесса.</param>
    /// <returns>
    /// <see langword="true"/>, если дерево процессов удалось завершить;
    /// иначе <see langword="false"/>.
    /// </returns>
    private bool TryKillProcessTree(Process process, int revitMajor)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            return true;
        }
        catch (Exception ex)
        {
            _launcherLog.Warn(
                "Не удалось принудительно остановить зависший Revit {0}: {1}",
                revitMajor,
                ex.Message);

            return false;
        }
    }

    /// <summary>
    /// Результат ожидания завершения внешнего процесса Revit.
    /// </summary>
    private enum ProcessWaitResult
    {
        Exited,
        KilledAfterFinalStatus,
        TimedOut
    }
}
