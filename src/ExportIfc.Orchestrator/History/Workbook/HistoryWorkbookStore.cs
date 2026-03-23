using ClosedXML.Excel;

using ExportIfc.Config;
using ExportIfc.Excel;
using ExportIfc.Logging;

namespace ExportIfc.History;

/// <summary>
/// Excel-реализация хранилища рабочей истории состояний моделей.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Читает строки истории из workbook-файла
///    <see cref="ProjectFileNames.HistoryWorkbook"/>.
/// 2. Сохраняет текущее состояние истории в Excel-книгу,
///    полностью пересобирая лист истории.
/// 3. Централизует работу с workbook-файлом,
///    не раскрывая orchestration-слою детали Excel.
///
/// Контракты:
/// 1. Отсутствие файла истории трактуется как пустая история,
///    а не как ошибка чтения.
/// 2. Отсутствие листа истории тоже трактуется как пустая история,
///    но логируется как предупреждение.
/// 3. При чтении используются только доменные данные:
///    путь к модели и время модификации с точностью до минуты.
/// 4. Пустые строки внутри занятого диапазона не обрывают чтение,
///    а просто пропускаются.
/// 5. При сохранении лист истории пересоздаётся целиком.
/// 6. Класс использует логгер подсистемы History для сообщений чтения и валидации.
/// </remarks>
internal sealed class HistoryWorkbookStore : IHistoryStore
{
    private readonly ConsoleLogger _historyLog = Log.For(LogComponents.History);

    /// <summary>
    /// Читает строки истории из Excel-книги.
    /// </summary>
    /// <param name="historyWorkbookPath">
    /// Полный путь к <see cref="ProjectFileNames.HistoryWorkbook"/>.
    /// </param>
    /// <param name="sheetName">Имя листа истории.</param>
    /// <returns>Прочитанные строки истории либо пустой список.</returns>
    /// <remarks>
    /// Workbook открывается в режиме shared-read через
    /// <see cref="ExcelWorkbookOpener.OpenForSharedRead(string, string)"/>,
    /// чтобы повысить шанс чтения файла, который уже открыт в Excel
    /// или другим процессом.
    ///
    /// Если workbook открыт в Excel с несохранёнными изменениями,
    /// будет доступна только последняя версия, сохранённая на диске.
    /// </remarks>
    public IReadOnlyList<HistoryRow> ReadRows(
        string historyWorkbookPath,
        string sheetName)
    {
        if (!File.Exists(historyWorkbookPath))
        {
            _historyLog.Info(
                "Файл {0} не найден, история считается пустой: '{1}'",
                ProjectFileNames.HistoryWorkbook,
                historyWorkbookPath);
            return [];
        }

        using var workbook = ExcelWorkbookOpener.OpenForSharedRead(
            historyWorkbookPath,
            ProjectFileNames.HistoryWorkbook);

        if (!workbook.TryGetWorksheet(sheetName, out var worksheet))
        {
            _historyLog.Warn(
                "В файле '{0}' не найден лист истории '{1}'. История считается пустой.",
                historyWorkbookPath,
                sheetName);
            return [];
        }

        var rows = ReadWorksheetRows(worksheet);

        _historyLog.Info(
            "В файле {0} найдено записей: {1}",
            ProjectFileNames.HistoryWorkbook,
            rows.Count);

        return rows;
    }

    /// <summary>
    /// Сохраняет строки истории в Excel-книгу.
    /// </summary>
    /// <param name="historyWorkbookPath">
    /// Полный путь к <see cref="ProjectFileNames.HistoryWorkbook"/>.
    /// </param>
    /// <param name="sheetName">Имя листа истории.</param>
    /// <param name="rows">Строки истории для сохранения.</param>
    /// <remarks>
    /// Метод создаёт каталог файла при необходимости,
    /// открывает существующую workbook-книгу либо создаёт новую,
    /// а затем делегирует пересборку листа специализированному writer'у.
    /// </remarks>
    public void Save(
        string historyWorkbookPath,
        string sheetName,
        IReadOnlyList<HistoryRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var directory = Path.GetDirectoryName(historyWorkbookPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        using var workbook = ExcelWorkbookOpener.OpenOrCreateForSave(
            historyWorkbookPath,
            ProjectFileNames.HistoryWorkbook,
            out var openedExistingFile);

        HistoryWorksheetWriter.RewriteSheet(workbook, sheetName, rows);

        if (openedExistingFile)
            workbook.Save();
        else
            workbook.SaveAs(historyWorkbookPath);
    }

    /// <summary>
    /// Читает доменные строки истории с Excel-листа.
    /// </summary>
    /// <param name="worksheet">Лист истории из workbook-файла.</param>
    /// <returns>Корректно прочитанные строки истории.</returns>
    /// <remarks>
    /// Метод идёт по занятому диапазону строк, начиная со второй строки.
    /// Первая строка диапазона содержит заголовок.
    ///
    /// Пустая строка внутри диапазона не считается концом данных.
    /// Некорректные записи пропускаются с предупреждением:
    /// это позволяет не ронять чтение всей истории из-за одной сломанной строки.
    /// </remarks>
    private List<HistoryRow> ReadWorksheetRows(IXLWorksheet worksheet)
    {
        var lastUsedRow = ExcelCells.GetLastUsedRowNumber(worksheet);
        if (lastUsedRow < 2)
            return [];

        var rows = new List<HistoryRow>();

        for (var rowNumber = 2; rowNumber <= lastUsedRow; rowNumber++)
        {
            var pathRaw = ExcelCells.GetCellText(
                worksheet,
                rowNumber,
                ExcelSchema.HistoryColRvtPath);

            var dateCell = worksheet.Cell(rowNumber, ExcelSchema.HistoryColDateTime);

            if (string.IsNullOrWhiteSpace(pathRaw) && dateCell.IsEmpty())
                continue;

            if (string.IsNullOrWhiteSpace(pathRaw))
            {
                _historyLog.Warn(
                    "Лист '{0}', строка {1}: запись пропущена, путь к RVT пустой.",
                    worksheet.Name,
                    rowNumber);
                continue;
            }

            if (!ExcelCells.TryReadDateTime(dateCell, out var lastModifiedMinute))
            {
                _historyLog.Warn(
                    "Лист '{0}', строка {1}: запись пропущена, дата модификации некорректна.",
                    worksheet.Name,
                    rowNumber);
                continue;
            }

            rows.Add(new HistoryRow(pathRaw, lastModifiedMinute));
        }

        return rows;
    }
}
