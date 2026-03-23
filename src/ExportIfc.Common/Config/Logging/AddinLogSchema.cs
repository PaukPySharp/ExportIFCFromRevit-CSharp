namespace ExportIfc.Config;

/// <summary>
/// Схема технических логов и файла статусов add-in.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует типы технических блоков add-in.
/// 2. Централизует префиксы строк для журнала статусов.
/// 3. Централизует базовые параметры оформления технического лога.
/// 4. Убирает размазывание служебных маркеров по add-in и оркестратору.
///
/// Контракты:
/// 1. Эти значения являются частью текстового формата техлогов add-in.
/// 2. Префиксы строк журнала статусов должны оставаться стабильными
///    для корректного чтения статусов обратной стороной.
/// 3. Длина разделителя задаёт визуальный формат блоков техлога.
/// </remarks>
public static class AddinLogSchema
{
    /// <summary>
    /// Тип блока для стартового техлога add-in.
    /// </summary>
    public const string StartupBlockType = "STARTUP";

    /// <summary>
    /// Тип блока для фатального техлога add-in.
    /// </summary>
    public const string FatalBlockType = "FATAL";

    /// <summary>
    /// Тип блока для журнала статусов add-in.
    /// </summary>
    public const string StatusBlockType = "STATUS";

    /// <summary>
    /// Префикс строки RunId в журнале статусов.
    /// </summary>
    public const string RunIdPrefix = "RunId=";

    /// <summary>
    /// Префикс строки RevitMajor в журнале статусов.
    /// </summary>
    public const string RevitMajorPrefix = "RevitMajor=";

    /// <summary>
    /// Префикс строки TaskFile в журнале статусов.
    /// </summary>
    public const string TaskFilePrefix = "TaskFile=";

    /// <summary>
    /// Префикс строки Status в журнале статусов.
    /// </summary>
    public const string StatusPrefix = "Status=";

    /// <summary>
    /// Префикс строки Message в журнале статусов.
    /// </summary>
    public const string MessagePrefix = "Message=";

    /// <summary>
    /// Метка RunId в заголовке технического блока.
    /// </summary>
    public const string HeaderRunIdLabel = "RunId";

    /// <summary>
    /// Метка версии Revit в заголовке технического блока.
    /// </summary>
    public const string HeaderRevitLabel = "Revit";

    /// <summary>
    /// Метка Task-файла в заголовке технического блока.
    /// </summary>
    public const string HeaderTaskLabel = "Task";

    /// <summary>
    /// Длина линии-разделителя в технических логах add-in.
    /// </summary>
    public const int BlockSeparatorLength = 100;
}