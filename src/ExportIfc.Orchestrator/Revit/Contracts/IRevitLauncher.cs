using ExportIfc.Config;

namespace ExportIfc.Revit;

/// <summary>
/// Контракт запуска внешнего пакетного процесса Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Отделяет оркестратор от конкретного способа запуска Revit.
/// 2. Скрывает детали ожидания завершения процесса и чтения итогового статуса add-in.
/// 3. Позволяет orchestration-слою работать с пакетным запуском как с одной операцией.
///
/// Контракты:
/// 1. Реализация должна запускать Revit для заданной major-версии.
/// 2. Возвращаемое значение отражает итог пакетного запуска:
///    процесс должен завершиться штатно, а add-in — подтвердить успех.
/// 3. Таймаут 0 или меньше означает ожидание без ограничения времени.
/// </remarks>
public interface IRevitLauncher
{
    /// <summary>
    /// Запускает Revit указанной версии и ждёт завершения пакетной обработки.
    /// </summary>
    /// <param name="revitMajor">Целевая major-версия Revit.</param>
    /// <param name="taskFilePath">Путь к временному файлу со списком моделей текущего пакета.</param>
    /// <param name="dirAdminData">Каталог <see cref="ProjectDirectoryNames.AdminData"/>.</param>
    /// <param name="iniPath">Путь к <see cref="ProjectFileNames.SettingsIni"/>.</param>
    /// <param name="runId">Идентификатор текущего запуска оркестратора.</param>
    /// <param name="timeoutMinutes">
    /// Таймаут ожидания batch-процесса в минутах.
    /// 0 или меньше — ждать без ограничения.
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если Revit завершился штатно и add-in подтвердил
    /// успешную обработку пакета; иначе <see langword="false"/>.
    /// </returns>
    bool RunAndWait(
        int revitMajor,
        string taskFilePath,
        string dirAdminData,
        string iniPath,
        string runId,
        int timeoutMinutes);
}