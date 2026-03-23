namespace ExportIfc.Config;

/// <summary>
/// Единая схема Excel: листы и индексы колонок.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Фиксирует структуру рабочих книг Excel
///    (<see cref="ProjectFileNames.ManageWorkbook"/>, <see cref="ProjectFileNames.HistoryWorkbook"/>).
/// 2. Описывает, какие листы и какие столбцы за что отвечают.
/// 3. Убирает «магические числа» и строковые заголовки из прикладного кода.
///
/// Контракты:
/// 1. Номера колонок задаются в Excel-совместимом 1-based виде (A=1, B=2, ...).
/// 2. Имена листов задаются во внешних настройках через <see cref="ProjectFileNames.SettingsIni"/>
///    и здесь не хардкодятся.
/// 3. Любые изменения структуры Excel отражаются в этом модуле,
///    чтобы не размазывать числовой контракт по коду.
/// </remarks>
public static class ExcelSchema
{
    // ===== Общие Excel-константы =====

    /// <summary>
    /// Excel number format: общий формат даты/времени для записи в ячейки.
    /// </summary>
    public const string DateTimeNumberFormat = "yyyy-mm-dd hh:mm";

    // ===== Основной рабочий лист =====

    /// <summary>
    /// A: Папка с RVT-моделями (обяз.).
    /// </summary>
    public const int ManageColRvtDir = 1;

    /// <summary>
    /// B: Папка выгрузки IFC с маппингом (обяз.).
    /// </summary>
    public const int ManageColOutMap = 2;

    /// <summary>
    /// C: Каталог с файлами настроек маппинга.
    /// </summary>
    public const int ManageColMapDir = 3;

    /// <summary>
    /// D: Имя TXT-файла сопоставления категорий Revit и классов IFC.
    /// </summary>
    public const int ManageColIfcClassMapping = 4;

    /// <summary>
    /// E: Папка выгрузки IFC без маппинга (опц.).
    /// </summary>
    public const int ManageColOutNoMap = 5;

    /// <summary>
    /// F: Имя JSON-файла для выгрузки без маппинга (опц.).
    /// </summary>
    public const int ManageColNoMapName = 6;

    // ===== Лист исключений =====

    /// <summary>
    /// A: Путь для исключения из обработки.
    /// </summary>
    public const int ManageIgnoreColPath = 1;

    // ===== Лист истории: заголовки и таблица =====

    /// <summary>
    /// Текст заголовка колонки пути к RVT (ячейка A1).
    /// </summary>
    public const string HistoryHeaderCol1 = "Файл RVT (полный путь)";

    /// <summary>
    /// Текст заголовка колонки даты модификации (ячейка B1).
    /// </summary>
    public const string HistoryHeaderCol2 = "Дата модификации файла";

    /// <summary>
    /// Стандартное имя Excel-таблицы на листе истории.
    /// </summary>
    public const string HistoryTableName = "HistoryTable";

    // ===== Лист истории: индексы колонок =====

    /// <summary>
    /// A: Полный путь к файлу RVT.
    /// </summary>
    public const int HistoryColRvtPath = 1;

    /// <summary>
    /// B: Дата модификации RVT, округлённая до минут.
    /// </summary>
    public const int HistoryColDateTime = 2;
}