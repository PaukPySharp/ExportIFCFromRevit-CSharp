namespace ExportIfc.Config;

/// <summary>
/// Имена ключевых файлов проекта и связанных с ними строковых контрактов.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует базовые и полные имена файлов проекта.
/// 2. Убирает смешение имён файлов с расширениями, форматами и каталогами.
/// 3. Делает контракт служебных файлов наглядным и легко находимым.
///
/// Контракты:
/// 1. Здесь хранятся только имена файлов и связанные с ними строковые маркеры.
/// 2. Абсолютные пути строятся в <see cref="ProjectFiles"/> и <see cref="ProjectPaths"/>.
/// 3. Полные имена строятся из базовых имён и <see cref="ProjectFileExtensions"/>.
/// </remarks>
public static class ProjectFileNames
{
    /// <summary>
    /// Базовое имя временного JSON-файла передачи.
    /// </summary>
    private const string _tmpBaseName = "tmp";

    /// <summary>
    /// Базовое имя управляющей Excel-книги моделей.
    /// </summary>
    private const string _manageBaseName = "manage";

    /// <summary>
    /// Базовое имя Excel-книги истории.
    /// </summary>
    private const string _historyBaseName = "history";

    /// <summary>
    /// Базовое имя основного ini-файла настроек.
    /// </summary>
    private const string _settingsBaseName = "settings";

    /// <summary>
    /// Полное имя основного ini-файла настроек.
    /// </summary>
    public const string SettingsIni = _settingsBaseName + ProjectFileExtensions.Ini;

    /// <summary>
    /// Полное имя временного JSON-файла передачи для боевого запуска.
    /// </summary>
    public const string TmpJson = _tmpBaseName + ProjectFileExtensions.Json;

    /// <summary>
    /// Префикс dry-run JSON-файлов пакетов по версиям Revit.
    /// </summary>
    public const string TmpDryRunFilePrefix = _tmpBaseName + "_";

    /// <summary>
    /// Полное имя управляющей Excel-книги моделей.
    /// </summary>
    public const string ManageWorkbook = _manageBaseName + ProjectFileExtensions.Xlsx;

    /// <summary>
    /// Полное имя Excel-книги истории.
    /// </summary>
    public const string HistoryWorkbook = _historyBaseName + ProjectFileExtensions.Xlsx;

    /// <summary>
    /// Префикс имени Task-файла для конкретной версии Revit.
    /// </summary>
    public const string TaskFilePrefix = "Task";

    /// <summary>
    /// Стандартное отображаемое имя Task-файла в логах и сообщениях.
    /// </summary>
    public const string TaskFileDisplayName = "Task-файл";
}
