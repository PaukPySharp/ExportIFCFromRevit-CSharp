using ExportIfc.Config;
using ExportIfc.Export.Diagnostics;
using ExportIfc.History;
using ExportIfc.IO;
using ExportIfc.Logging;
using ExportIfc.Tests.TestInfrastructure;

using Xunit;

namespace ExportIfc.Tests.Export.Diagnostics;

/// <summary>
/// Проверяет запись диагностических артефактов оркестратора и best-effort сохранение истории.
/// </summary>
/// <remarks>
/// Сьют фиксирует два важных контракта:
/// distinct/sorted вывод для mtime-диагностики и отсутствие исключения наружу,
/// если persistence-слой истории не смог сохранить workbook.
/// </remarks>
public sealed class ExportDiagnosticsWriterTests
{
    [Fact]
    public void WriteMTimeIssues_WritesDistinctSortedPaths_WithoutSeparator()
    {
        using var workspace = TestWorkspace.Create();

        var dirLogs = workspace.CreateDirectory("logs");
        var sut = new ExportDiagnosticsWriter(new StubHistoryStore());
        var exportLog = new ConsoleLogger("test");

        var mtimeIssues = new[]
        {
            @"C:\Models\B.rvt",
            @"c:\models\a.rvt",
            @"c:\MODELS\b.rvt",
            @"C:\MODELS\A.rvt"
        };

        sut.WriteMTimeIssues(dirLogs, mtimeIssues, exportLog);

        var logPath = TextLogs.BuildPath(dirLogs, LogFiles.MTimeIssues, addDateSuffix: true);

        Assert.True(File.Exists(logPath));
        Assert.Equal(
            new[]
            {
                @"c:\models\a.rvt",
                @"C:\Models\B.rvt"
            },
            File.ReadAllLines(logPath));
    }

    [Fact]
    public void TrySaveHistory_ReturnsFalse_WhenHistoryStoreThrows()
    {
        var store = new StubHistoryStore
        {
            ThrowOnSave = true
        };

        var sut = new ExportDiagnosticsWriter(store);
        var history = HistoryManager.FromRows(
            [
                new HistoryRow(@"C:\Models\A.rvt", new DateTime(2026, 3, 8, 10, 0, 0))
            ]);

        var ok = sut.TrySaveHistory(
            history,
            @"C:\Temp\history.xlsx",
            "History",
            new ConsoleLogger("test"));

        Assert.False(ok);
    }

    /// <summary>
    /// Минимальная test-double реализация <see cref="IHistoryStore"/>.
    /// </summary>
    /// <remarks>
    /// Используется, когда сьюту важно проверить реакцию вызывающего кода
    /// на отказ persistence-слоя, а не сам Excel-контракт истории.
    /// </remarks>
    private sealed class StubHistoryStore : IHistoryStore
    {
        /// <summary>
        /// Получает или задаёт признак искусственного сбоя при сохранении.
        /// </summary>
        public bool ThrowOnSave { get; set; }

        public IReadOnlyList<HistoryRow> ReadRows(string historyWorkbookPath, string sheetName)
            => Array.Empty<HistoryRow>();

        public void Save(
            string historyWorkbookPath,
            string sheetName,
            IReadOnlyList<HistoryRow> rows)
        {
            if (ThrowOnSave)
                throw new IOException("boom");
        }
    }
}
