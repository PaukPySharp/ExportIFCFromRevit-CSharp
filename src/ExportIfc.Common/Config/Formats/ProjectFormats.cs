namespace ExportIfc.Config;

/// <summary>
/// Форматы строкового представления дат и времени в проекте.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует форматы дат и времени для логов, консоли и служебных идентификаторов.
/// 2. Убирает размазывание шаблонов форматирования по коду.
/// 3. Делает строковые контракты дат наглядными и единообразными.
///
/// Контракты:
/// 1. Изменение любого формата влияет на внешний вид строк,
///    имён файлов или идентификаторов запуска.
/// 2. Эти значения не являются пользовательскими настройками и не читаются из ini.
/// </remarks>
public static class ProjectFormats
{
    /// <summary>
    /// Формат даты и времени для консоли и технических логов.
    /// </summary>
    public const string DateTimeDisplay = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// Формат даты и времени с точностью до минуты для текстового представления.
    /// </summary>
    public const string DateTimeMinuteText = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// Формат времени внутри коротких строк технического лога add-in.
    /// </summary>
    public const string TimeOnly = "HH:mm:ss";

    /// <summary>
    /// Формат даты для имён файлов логов.
    /// </summary>
    public const string LogDate = "yyyy.MM.dd";

    /// <summary>
    /// Формат даты и времени для идентификатора запуска оркестратора.
    /// </summary>
    public const string RunId = "yyyyMMdd_HHmmss_fff";
}
