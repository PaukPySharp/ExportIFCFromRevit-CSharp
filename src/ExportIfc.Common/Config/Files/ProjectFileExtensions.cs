namespace ExportIfc.Config;

/// <summary>
/// Стандартные расширения файлов проекта.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует расширения, используемые в путях, проверках и именовании файлов.
/// 2. Убирает строковые литералы вида ".json" и ".txt" из прикладного кода.
/// 3. Делает правила формирования имён файлов предсказуемыми.
///
/// Контракты:
/// 1. Все значения задаются с ведущей точкой.
/// 2. Расширения используются как часть строкового контракта имён файлов.
/// 3. Изменение этих констант влияет на формирование имён по всему проекту.
/// </remarks>
public static class ProjectFileExtensions
{
    /// <summary>
    /// Расширение IFC-файлов.
    /// </summary>
    public const string Ifc = ".ifc";

    /// <summary>
    /// Расширение INI-файлов.
    /// </summary>
    public const string Ini = ".ini";

    /// <summary>
    /// Расширение JSON-файлов проекта.
    /// </summary>
    public const string Json = ".json";

    /// <summary>
    /// Расширение RVT-файлов.
    /// </summary>
    public const string Rvt = ".rvt";

    /// <summary>
    /// Расширение текстовых файлов проекта.
    /// </summary>
    public const string Txt = ".txt";

    /// <summary>
    /// Расширение Excel-книг проекта.
    /// </summary>
    public const string Xlsx = ".xlsx";
}