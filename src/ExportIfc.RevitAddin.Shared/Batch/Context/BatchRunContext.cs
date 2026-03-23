using ExportIfc.Config;

namespace ExportIfc.RevitAddin.Batch.Context;

/// <summary>
/// Контекст одного пакетного запуска add-in.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Хранит итоговые runtime-значения текущего batch-запуска.
/// 2. Передаёт подготовленные пути и параметры экспорта между этапами выполнения.
///
/// Контракты:
/// 1. Экземпляр содержит только уже собранные и готовые к использованию значения.
/// 2. Путь к <see cref="ProjectFileNames.SettingsIni"/> обязателен для корректного batch-запуска.
/// 3. Класс не читает окружение и не содержит логики валидации batch-запуска.
/// </remarks>
internal sealed class BatchRunContext
{
    /// <summary>
    /// Создаёт контекст пакетного запуска.
    /// </summary>
    /// <param name="dirAdminData">Каталог <see cref="ProjectDirectoryNames.AdminData"/> текущего запуска.</param>
    /// <param name="taskFilePath">Путь к файлу списка моделей текущего пакетного запуска.</param>
    /// <param name="iniPath">Путь к <see cref="ProjectFileNames.SettingsIni"/> текущего запуска.</param>
    /// <param name="runId">Идентификатор текущего пакетного запуска.</param>
    /// <param name="revitMajor">Целевая major-версия Revit текущего пакетного запуска.</param>
    /// <param name="tmpJsonPath">Путь к файлу <see cref="ProjectFileNames.TmpJson"/>.</param>
    /// <param name="exportViewName">Имя 3D-вида Revit для экспорта IFC.</param>
    /// <param name="enableUnmappedExport">Признак разрешённой выгрузки без маппинга.</param>
    public BatchRunContext(
        string dirAdminData,
        string taskFilePath,
        string iniPath,
        string runId,
        int revitMajor,
        string tmpJsonPath,
        string exportViewName,
        bool enableUnmappedExport)
    {
        DirAdminData = dirAdminData ?? throw new ArgumentNullException(nameof(dirAdminData));
        TaskFilePath = taskFilePath ?? throw new ArgumentNullException(nameof(taskFilePath));
        IniPath = iniPath ?? throw new ArgumentNullException(nameof(iniPath));
        RunId = runId ?? throw new ArgumentNullException(nameof(runId));
        RevitMajor = revitMajor > 0
            ? revitMajor
            : throw new ArgumentOutOfRangeException(nameof(revitMajor));
        TmpJsonPath = tmpJsonPath ?? throw new ArgumentNullException(nameof(tmpJsonPath));
        ExportViewName = exportViewName ?? throw new ArgumentNullException(nameof(exportViewName));
        EnableUnmappedExport = enableUnmappedExport;
    }

    /// <summary>
    /// Каталог <see cref="ProjectDirectoryNames.AdminData"/> текущего запуска.
    /// </summary>
    public string DirAdminData { get; }

    /// <summary>
    /// Путь к файлу списка моделей текущего пакетного запуска.
    /// </summary>
    public string TaskFilePath { get; }

    /// <summary>
    /// Путь к обязательному <see cref="ProjectFileNames.SettingsIni"/> текущего запуска.
    /// </summary>
    public string IniPath { get; }

    /// <summary>
    /// Идентификатор текущего пакетного запуска.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Целевая major-версия Revit текущего пакетного запуска.
    /// </summary>
    public int RevitMajor { get; }

    /// <summary>
    /// Путь к файлу <see cref="ProjectFileNames.TmpJson"/>.
    /// </summary>
    public string TmpJsonPath { get; }

    /// <summary>
    /// Имя 3D-вида Revit для экспорта IFC.
    /// </summary>
    public string ExportViewName { get; }

    /// <summary>
    /// Признак разрешённой выгрузки без маппинга.
    /// </summary>
    public bool EnableUnmappedExport { get; }
}
