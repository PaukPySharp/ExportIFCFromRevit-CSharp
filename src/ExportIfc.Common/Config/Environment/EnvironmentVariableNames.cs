namespace ExportIfc.Config;

/// <summary>
/// Имена переменных окружения проекта ExportIFC.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует имена переменных окружения, используемых между процессами.
/// 2. Убирает строковые литералы из оркестратора, add-in и общих библиотек.
/// 3. Делает межпроцессный контракт отдельно находимым.
///
/// Контракты:
/// 1. Здесь хранятся только имена переменных окружения, без их значений.
/// 2. Значения для флагов и переключателей задаются в <see cref="EnvironmentVariableValues"/>.
/// 3. Изменение имён влияет на обмен данными между процессами.
/// </remarks>
public static class EnvironmentVariableNames
{
    /// <summary>
    /// Явный флаг пакетного автозапуска add-in.
    /// </summary>
    public const string Autorun = "EXPORTIFC_AUTORUN";

    /// <summary>
    /// Флаг ожидания ручного подключения debugger к процессу Revit.
    /// </summary>
    public const string DebugWaitAttach = "EXPORTIFC_DEBUG_WAIT_ATTACH";

    /// <summary>
    /// Каталог <see cref="ProjectDirectoryNames.AdminData"/>.
    /// </summary>
    public const string AdminData = "EXPORTIFC_ADMIN_DATA";

    /// <summary>
    /// Путь к файлу списка моделей текущего пакетного запуска.
    /// </summary>
    public const string TaskFile = "EXPORTIFC_TASK_FILE";

    /// <summary>
    /// Путь к <see cref="ProjectFileNames.SettingsIni"/>.
    /// </summary>
    public const string SettingsIni = "EXPORTIFC_SETTINGS_INI";

    /// <summary>
    /// Уникальный идентификатор запуска.
    /// </summary>
    public const string RunId = "EXPORTIFC_RUN_ID";

    /// <summary>
    /// Целевая major-версия Revit для текущего пакетного запуска.
    /// </summary>
    public const string RevitMajor = "EXPORTIFC_REVIT_MAJOR";

    /// <summary>
    /// Путь к <see cref="RevitConstants.ExecutableFileName"/> для переопределения запуска.
    /// </summary>
    public const string RevitExe = "EXPORTIFC_REVIT_EXE";
}