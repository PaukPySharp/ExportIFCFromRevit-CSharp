using ClosedXML.Excel;

using ExportIfc.Config;

namespace ExportIfc.Excel;

/// <summary>
/// Вспомогательные операции для чтения Excel через ClosedXML.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизовать чтение строковых ячеек.
/// 2. Централизовать проверку пустых строк.
/// 3. Централизовать безопасный разбор даты/времени из ячейки.
///
/// Контракты:
/// 1. Номера столбцов передаются в Excel-совместимом 1-based виде.
/// 2. Пустыми считаются значения null, пустая строка и строка из пробелов.
/// 3. При ошибке разбора даты исключения наружу не выбрасываются.
/// </remarks>
internal static class ExcelCells
{
    /// <summary>
    /// Возвращает содержимое ячейки как строку без пробелов по краям.
    /// </summary>
    /// <param name="worksheet">Лист Excel.</param>
    /// <param name="row">Номер строки, начиная с 1.</param>
    /// <param name="column">Номер столбца Excel, начиная с 1.</param>
    /// <returns>Строковое значение ячейки.</returns>
    public static string GetCellText(
        IXLWorksheet worksheet,
        int row,
        int column)
    {
        return worksheet
            .Cell(row, column)
            .GetString()
            .Trim();
    }

    /// <summary>
    /// Возвращает номер последней занятой строки листа.
    /// </summary>
    /// <param name="worksheet">Лист Excel.</param>
    /// <returns>
    /// Номер последней занятой строки либо 1,
    /// если на листе нет занятых строк.
    /// </returns>
    /// <remarks>
    /// Метод удобен для сценариев, где лист читается по занятому диапазону,
    /// а не до первой пустой строки внутри данных.
    /// </remarks>
    public static int GetLastUsedRowNumber(IXLWorksheet worksheet)
        => worksheet.LastRowUsed()?.RowNumber() ?? 1;

    /// <summary>
    /// Проверяет, что все переданные значения пусты.
    /// </summary>
    /// <param name="values">Набор значений строки.</param>
    /// <returns>
    /// <see langword="true"/>, если все значения пусты;
    /// иначе <see langword="false"/>.
    /// </returns>
    public static bool IsBlankRow(params string[] values)
        => values.All(string.IsNullOrWhiteSpace);

    /// <summary>
    /// Пытается разобрать дату/время из ячейки.
    /// </summary>
    /// <param name="cell">Ячейка Excel.</param>
    /// <param name="dateTime">Результат разбора.</param>
    /// <returns>
    /// <see langword="true"/>, если дата успешно разобрана;
    /// иначе <see langword="false"/>.
    /// </returns>
    public static bool TryReadDateTime(IXLCell cell, out DateTime dateTime)
    {
        if (cell.DataType == XLDataType.DateTime)
        {
            dateTime = cell.GetDateTime();
            return true;
        }

        if (cell.DataType == XLDataType.Number
            && cell.TryGetValue<double>(out var oaDate))
        {
            try
            {
                dateTime = DateTime.FromOADate(oaDate);
                return true;
            }
            catch (ArgumentException)
            {
                dateTime = default;
                return false;
            }
        }

        var raw = cell.GetString().Trim();

        if (DateTime.TryParseExact(
                raw,
                ProjectFormats.DateTimeMinuteText,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out dateTime))
        {
            return true;
        }

        if (DateTime.TryParse(raw, out dateTime))
            return true;

        dateTime = default;
        return false;
    }
}