using ExportIfc.Config;
using ExportIfc.Settings;

namespace ExportIfc.Manage;

/// <summary>
/// Контракт загрузки управляющей Excel-книги
/// <see cref="ProjectFileNames.ManageWorkbook"/>.
/// </summary>
/// <remarks>
/// Назначение:
/// Отделяет orchestration-логику от конкретной реализации чтения Excel,
/// разбора листов Path и ignore, а также сбора списка моделей для экспорта.
///
/// Контракты:
/// 1. Отсутствие файла <see cref="ProjectFileNames.ManageWorkbook"/> считается фатальной ошибкой.
/// 2. Отсутствие обязательного листа Path считается фатальной ошибкой конфигурации.
/// 3. Отсутствие листа ignore не приводит к исключению:
///    загрузчик пишет предупреждение и продолжает работу без ignore-списка.
/// 4. Для доступа к рабочим каталогам используются уже подготовленные runtime-пути,
///    а не сырые строковые значения из настроек.
/// 5. Ошибки обязательных конфигурационных файлов для строк Path считаются фатальными
///    и не замалчиваются внутри загрузчика.
/// </remarks>
public interface IManageWorkbookLoader
{
    /// <summary>
    /// Загружает данные из управляющей Excel-книги моделей.
    /// </summary>
    /// <param name="manageXlsxPath">Полный путь к <see cref="ProjectFileNames.ManageWorkbook"/>.</param>
    /// <param name="stg">Итоговые настройки приложения.</param>
    /// <param name="paths">
    /// Подготовленные runtime-пути проекта.
    /// Для зависимых конфигурационных файлов используется
    /// <see cref="ProjectPaths.DirExportConfig"/>.
    /// </param>
    /// <returns>Собранные данные для дальнейшей оркестрации.</returns>
    ManageWorkbookData Load(
        string manageXlsxPath,
        AppSettings stg,
        ProjectPaths paths);
}
