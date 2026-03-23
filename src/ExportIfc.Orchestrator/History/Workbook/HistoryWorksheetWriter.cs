using ClosedXML.Excel;

using ExportIfc.Config;

namespace ExportIfc.History;

/// <summary>
/// Запись и оформление листа истории в Excel-книге.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Пересоздаёт лист истории в workbook-книге.
/// 2. Записывает заголовок и строки истории.
/// 3. Применяет табличное оформление листа.
///
/// Контракты:
/// 1. Метод всегда создаёт новый пустой лист истории с заданным именем.
/// 2. Даже при пустом наборе строк на листе создаётся одна пустая строка данных.
/// 3. Формат даты и структура таблицы задаются централизованно через <see cref="ExcelSchema"/>.
/// 4. Класс отвечает только за layout и запись листа, но не открывает и не сохраняет workbook-файл.
/// </remarks>
internal static class HistoryWorksheetWriter
{
    /// <summary>
    /// Пересобирает лист истории в workbook-книге.
    /// </summary>
    /// <param name="workbook">Целевая Excel-книга.</param>
    /// <param name="sheetName">Имя листа истории.</param>
    /// <param name="rows">Строки истории для записи.</param>
    public static void RewriteSheet(
        XLWorkbook workbook,
        string sheetName,
        IReadOnlyList<HistoryRow> rows)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(rows);

        var worksheet = RecreateHistorySheet(workbook, sheetName);

        WriteHeader(worksheet);
        WriteBody(worksheet, rows);
        FormatWorksheet(worksheet);
    }

    /// <summary>
    /// Пересоздаёт лист истории в workbook-книге.
    /// </summary>
    /// <param name="workbook">Целевая Excel-книга.</param>
    /// <param name="sheetName">Имя листа истории.</param>
    /// <returns>Новый пустой лист истории.</returns>
    /// <remarks>
    /// Метод гарантирует, что дальнейшая запись
    /// выполняется в новый пустой лист с заданным именем.
    /// </remarks>
    private static IXLWorksheet RecreateHistorySheet(
        XLWorkbook workbook,
        string sheetName)
    {
        if (workbook.TryGetWorksheet(sheetName, out var existing))
            existing.Delete();

        return workbook.AddWorksheet(sheetName);
    }

    /// <summary>
    /// Записывает строку заголовков листа истории.
    /// </summary>
    /// <param name="worksheet">Лист истории.</param>
    /// <remarks>
    /// Тексты заголовков берутся из <see cref="ExcelSchema"/>,
    /// чтобы структура workbook не размазывалась строковыми литералами по коду.
    /// </remarks>
    private static void WriteHeader(IXLWorksheet worksheet)
    {
        var col1 = ExcelSchema.HistoryColRvtPath;
        var col2 = ExcelSchema.HistoryColDateTime;

        worksheet.Cell(1, col1).Value = ExcelSchema.HistoryHeaderCol1;
        worksheet.Cell(1, col2).Value = ExcelSchema.HistoryHeaderCol2;

        var header = worksheet.Range(1, col1, 1, col2);
        header.Style.Font.Bold = true;
        header.Style.Font.FontSize = 14;
        header.Style.Font.FontColor = XLColor.Black;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    /// <summary>
    /// Записывает тело листа истории.
    /// </summary>
    /// <param name="worksheet">Лист истории.</param>
    /// <param name="rows">Строки истории для записи.</param>
    /// <remarks>
    /// Даже при пустой истории метод оставляет на листе пустую строку данных,
    /// чтобы последующее форматирование, расчёт диапазона и создание Excel-таблицы
    /// работали по одному и тому же сценарию без специальных веток.
    /// </remarks>
    private static void WriteBody(
        IXLWorksheet worksheet,
        IReadOnlyList<HistoryRow> rows)
    {
        var col1 = ExcelSchema.HistoryColRvtPath;
        var col2 = ExcelSchema.HistoryColDateTime;

        if (rows.Count == 0)
        {
            worksheet.Cell(2, col1).Value = string.Empty;
            worksheet.Cell(2, col2).Value = string.Empty;
            return;
        }

        var excelRow = 2;
        foreach (var row in rows)
        {
            worksheet.Cell(excelRow, col1).Value = row.Path;
            worksheet.Cell(excelRow, col2).Value = row.LastModifiedMinute;
            excelRow++;
        }
    }

    /// <summary>
    /// Применяет оформление и табличный формат к листу истории.
    /// </summary>
    /// <param name="worksheet">Лист истории.</param>
    /// <remarks>
    /// Метод задаёт ширины столбцов, оформляет диапазон данных и превращает занятый
    /// диапазон в Excel-таблицу. Стили для тела листа применяются диапазонами,
    /// а не по каждой ячейке отдельно: для ClosedXML это проще, чище и не дублирует логику.
    /// </remarks>
    private static void FormatWorksheet(IXLWorksheet worksheet)
    {
        var col1 = ExcelSchema.HistoryColRvtPath;
        var col2 = ExcelSchema.HistoryColDateTime;

        worksheet.Column(col1).Width = 150;
        worksheet.Column(col2).Width = 40;

        // После WriteBody в листе гарантированно существует хотя бы одна строка данных:
        // либо реальная запись истории, либо пустая строка-заглушка для стабильной структуры.
        var lastRow = Math.Max(2, worksheet.LastRowUsed()?.RowNumber() ?? 2);

        // Стили тела листа применяются одним диапазоном.
        // Это убирает поячейочный дубль и лучше соответствует модели ClosedXML.
        var bodyRange = worksheet.Range(2, col1, lastRow, col2);
        bodyRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        // Формат даты задаётся сразу всему столбцу данных с датой.
        // Даже если история пустая, формат остаётся стабильным.
        var dateRange = worksheet.Range(2, col2, lastRow, col2);
        dateRange.Style.DateFormat.Format = ExcelSchema.DateTimeNumberFormat;

        var tableRange = worksheet.Range(1, col1, lastRow, col2);
        var table = tableRange.CreateTable(ExcelSchema.HistoryTableName);

        table.Theme = XLTableTheme.TableStyleMedium2;
        table.ShowAutoFilter = true;
    }
}