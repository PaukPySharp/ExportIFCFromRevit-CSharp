using ClosedXML.Excel;

using ExportIfc.Config;
using ExportIfc.History;
using ExportIfc.Tests.TestInfrastructure;

using Xunit;

namespace ExportIfc.Tests.History.Workbook;

/// <summary>
/// Проверяет Excel-перенос истории моделей между workbook и domain-снимком.
/// </summary>
/// <remarks>
/// Сьют закрывает не только happy path, но и мусорные строки внутри листа,
/// чтобы reader не обрывал чтение из-за случайных пустот и битых записей.
/// </remarks>
public sealed class HistoryWorkbookStoreTests
{
    [Fact]
    public void ReadRows_ReturnsEmpty_WhenWorkbookIsMissing()
    {
        using var workspace = TestWorkspace.Create();

        var historyWorkbookPath = workspace.GetPath("missing-history.xlsx");
        var sut = new HistoryWorkbookStore();

        var rows = sut.ReadRows(historyWorkbookPath, "History");

        Assert.Empty(rows);
    }

    [Fact]
    public void ReadRows_SkipsBrokenRows_AndDoesNotStopOnBlankRows()
    {
        using var workspace = TestWorkspace.Create();

        var historyWorkbookPath = workspace.GetPath("history.xlsx");
        var dtA = new DateTime(2026, 3, 8, 10, 0, 0);
        var dtB = new DateTime(2026, 3, 8, 11, 30, 0);

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("History");

            worksheet.Cell(1, ExcelSchema.HistoryColRvtPath).Value = ExcelSchema.HistoryHeaderCol1;
            worksheet.Cell(1, ExcelSchema.HistoryColDateTime).Value = ExcelSchema.HistoryHeaderCol2;

            worksheet.Cell(2, ExcelSchema.HistoryColRvtPath).Value = @"C:\Models\A.rvt";
            worksheet.Cell(2, ExcelSchema.HistoryColDateTime).Value = dtA;

            // Полностью пустая строка внутри диапазона не должна обрывать чтение.
            worksheet.Cell(4, ExcelSchema.HistoryColRvtPath).Value = @"C:\Models\MissingDate.rvt";
            worksheet.Cell(5, ExcelSchema.HistoryColDateTime).Value = new DateTime(2026, 3, 8, 11, 0, 0);

            worksheet.Cell(6, ExcelSchema.HistoryColRvtPath).Value = @"C:\Models\B.rvt";
            worksheet.Cell(6, ExcelSchema.HistoryColDateTime).Value = dtB;

            workbook.SaveAs(historyWorkbookPath);
        }

        var sut = new HistoryWorkbookStore();

        var rows = sut.ReadRows(historyWorkbookPath, "History");

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.Path == @"C:\Models\A.rvt" && row.LastModifiedMinute == dtA);
        Assert.Contains(rows, row => row.Path == @"C:\Models\B.rvt" && row.LastModifiedMinute == dtB);
    }

    [Fact]
    public void Save_CreatesWorkbook_AndRoundTripsRows()
    {
        using var workspace = TestWorkspace.Create();

        var historyWorkbookPath = workspace.GetPath("nested", "history.xlsx");
        var expectedRows = new[]
        {
            new HistoryRow(@"C:\Models\B.rvt", new DateTime(2026, 3, 8, 11, 30, 0)),
            new HistoryRow(@"C:\Models\A.rvt", new DateTime(2026, 3, 8, 10, 0, 0))
        };

        var sut = new HistoryWorkbookStore();

        sut.Save(historyWorkbookPath, "History", expectedRows);

        Assert.True(File.Exists(historyWorkbookPath));

        var actualRows = sut.ReadRows(historyWorkbookPath, "History");

        Assert.Equal(expectedRows, actualRows);
    }

    [Fact]
    public void Save_RoundTripsEmptyHistory_AsEmptyDomainSnapshot()
    {
        using var workspace = TestWorkspace.Create();

        var historyWorkbookPath = workspace.GetPath("empty-history.xlsx");
        var sut = new HistoryWorkbookStore();

        sut.Save(historyWorkbookPath, "History", Array.Empty<HistoryRow>());

        var actualRows = sut.ReadRows(historyWorkbookPath, "History");

        // Excel-лист физически содержит пустую строку-заглушку,
        // но доменная история после чтения должна оставаться пустой.
        Assert.Empty(actualRows);
    }
}
