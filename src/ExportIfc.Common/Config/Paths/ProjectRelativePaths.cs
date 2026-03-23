namespace ExportIfc.Config;

/// <summary>
/// Относительные пути к ключевым объектам проекта.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует готовые относительные пути, используемые в поиске, инициализации и сообщениях.
/// 2. Убирает повторяющуюся склейку строк из клиентского кода.
/// 3. Делает контракты размещения служебных файлов и каталогов явными.
///
/// Контракты:
/// 1. Пути задаются относительно корня проекта.
/// 2. Здесь хранятся именно относительные пути, а не абсолютные значения.
/// 3. Пути для отображения фиксируются в slash-формате и не зависят от платформенного разделителя каталогов.
/// </remarks>
public static class ProjectRelativePaths
{
    /// <summary>
    /// Относительный путь к основному ini-файлу настроек.
    /// </summary>
    public const string SettingsIni = ProjectDirectoryNames.Settings + "/" + ProjectFileNames.SettingsIni;

    /// <summary>
    /// Относительный пользовательский путь к каталогу логов.
    /// </summary>
    public const string LogsRelativeDisplayPath = ProjectDirectoryNames.AdminData + "/" + LogDirectoryNames.Logs;
}