namespace ExportIfc.Manage;

/// <summary>
/// Человекочитаемые описания полей и файловых сущностей строки листа Path.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует тексты для логов, исключений и сообщений валидации.
/// 2. Убирает дубли строковых литералов из parser и resolver.
/// 3. Снижает риск расхождения формулировок при дальнейшем сопровождении.
///
/// Контракты:
/// 1. Значения используются только для внутренних диагностических сообщений.
/// 2. Формулировки должны описывать смысл поля, а не дублировать имена переменных.
/// </remarks>
internal static class ManagePathRowDescriptions
{
    /// <summary>
    /// Каталог с моделями Revit.
    /// </summary>
    public const string ModelsDirectory = "каталог с моделями";

    /// <summary>
    /// Каталог выгрузки с маппингом.
    /// </summary>
    public const string OutputDirMapping = "каталог выгрузки с маппингом";

    /// <summary>
    /// Каталог с файлами настроек маппинга.
    /// </summary>
    public const string MappingSettingsDirectory = "каталог с файлами настроек маппинга";

    /// <summary>
    /// Имя файла сопоставления категорий Revit и классов IFC.
    /// </summary>
    public const string IfcClassMappingFileName = "имя файла сопоставления категорий Revit и классов IFC";

    /// <summary>
    /// Файл сопоставления категорий Revit и классов IFC.
    /// </summary>
    public const string IfcClassMappingFile = "файл сопоставления категорий Revit и классов IFC";

    /// <summary>
    /// Каталог выгрузки без маппинга.
    /// </summary>
    public const string OutputDirNoMap = "каталог выгрузки без маппинга";

    /// <summary>
    /// Имя JSON-файла выгрузки без маппинга.
    /// </summary>
    public const string NoMapJsonFileName = "имя JSON-файла выгрузки без маппинга";

    /// <summary>
    /// JSON-файл настроек выгрузки с маппингом.
    /// </summary>
    public const string MappingJsonFile = "JSON-файл настроек выгрузки с маппингом";

    /// <summary>
    /// JSON-файл настроек выгрузки без маппинга.
    /// </summary>
    public const string NoMapJsonFile = "JSON-файл настроек выгрузки без маппинга";

    /// <summary>
    /// Целевая папка выгрузки.
    /// </summary>
    public const string OutputDirectory = "целевая папка";
}