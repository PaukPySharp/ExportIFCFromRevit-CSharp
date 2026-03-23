using ExportIfc.Config;

namespace ExportIfc.History;

/// <summary>
/// Контракт чтения и записи Excel-книги истории
/// <see cref="ProjectFileNames.HistoryWorkbook"/>.
/// </summary>
/// <remarks>
/// Назначение:
/// Отделяет доменную модель истории от конкретной реализации
/// хранения в Excel-книге.
///
/// Контракты:
/// 1. Чтение возвращает доменные строки истории, а не Excel-объекты.
/// 2. Отсутствие файла или листа истории может трактоваться реализацией как пустая история.
/// 3. Интерфейс не навязывает вызывающему коду детали структуры workbook-файла.
/// </remarks>
internal interface IHistoryStore
{
    /// <summary>
    /// Читает строки истории из Excel-книги.
    /// </summary>
    /// <param name="historyWorkbookPath">
    /// Полный путь к <see cref="ProjectFileNames.HistoryWorkbook"/>.
    /// </param>
    /// <param name="sheetName">Имя листа истории.</param>
    /// <returns>Прочитанные строки истории.</returns>
    IReadOnlyList<HistoryRow> ReadRows(
        string historyWorkbookPath,
        string sheetName);

    /// <summary>
    /// Сохраняет строки истории в Excel-книгу.
    /// </summary>
    /// <param name="historyWorkbookPath">
    /// Полный путь к <see cref="ProjectFileNames.HistoryWorkbook"/>.
    /// </param>
    /// <param name="sheetName">Имя листа истории.</param>
    /// <param name="rows">Строки истории для сохранения.</param>
    void Save(
        string historyWorkbookPath,
        string sheetName,
        IReadOnlyList<HistoryRow> rows);
}