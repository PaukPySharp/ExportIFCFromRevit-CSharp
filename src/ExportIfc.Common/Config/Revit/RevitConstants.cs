namespace ExportIfc.Config;

/// <summary>
/// Строковые константы, относящиеся к интеграции с Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует имена файлов, аргументы запуска и служебные строки Revit-сценариев.
/// 2. Убирает Revit-специфичные литералы из общего набора файловых констант.
/// 3. Делает интеграционный контракт с Revit отдельно находимым.
///
/// Контракты:
/// 1. Эти значения относятся к запуску Revit и работе add-in.
/// 2. Пути здесь задаются только как строковые шаблоны и не создают файлов.
/// 3. Изменение этих значений может повлиять на запуск batch-сценариев и экспорт IFC.
/// </remarks>
public static class RevitConstants
{
    /// <summary>
    /// Имя исполняемого файла Revit.
    /// </summary>
    public const string ExecutableFileName = "Revit.exe";

    /// <summary>
    /// Аргументы запуска Revit для batch-сценария.
    /// </summary>
    public const string NoSplashArguments = "/nosplash";

    /// <summary>
    /// Имя транзакции Revit для экспорта IFC.
    /// </summary>
    public const string IfcExportTransactionName = "Export IFC by PaukPySharp";
}