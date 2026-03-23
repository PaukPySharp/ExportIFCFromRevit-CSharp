using ExportIfc.Config;
using ExportIfc.Export;
using ExportIfc.Export.Diagnostics;
using ExportIfc.Export.Planning;
using ExportIfc.Export.Runtime;
using ExportIfc.Export.Selection;
using ExportIfc.History;
using ExportIfc.Manage;
using ExportIfc.Models;
using ExportIfc.Revit;
using ExportIfc.Settings;
using ExportIfc.Tests.TestInfrastructure;
using ExportIfc.Transfer;
using ExportIfc.Validation;

using Xunit;

namespace ExportIfc.Tests.Export;

/// <summary>
/// Проверяет итоговую семантику завершения orchestration-прогона.
/// </summary>
public sealed class ExportOrchestratorTests
{
    [Fact]
    public void Run_ReturnsSuccess_WhenSelectedModelsArePlannedAndProcessedWithoutErrors()
    {
        using var workspace = TestWorkspace.Create();

        var orchestrator = CreateOrchestrator(
            workspace,
            detectRevitMajor: _ => 2022,
            models:
            [
                TestModelFactory.Create(
                    workspace.GetPath("A.rvt"),
                    new DateTime(2026, 3, 8, 10, 0, 0),
                    outputDirMapping: workspace.CreateDirectory("out-map"))
            ]);

        var exitCode = orchestrator.Run();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Run_ReturnsFailure_WhenBatchPlanContainsVersionDiagnostics()
    {
        using var workspace = TestWorkspace.Create();

        var orchestrator = CreateOrchestrator(
            workspace,
            detectRevitMajor: _ => null,
            models:
            [
                TestModelFactory.Create(
                    workspace.GetPath("A.rvt"),
                    new DateTime(2026, 3, 8, 10, 0, 0),
                    outputDirMapping: workspace.CreateDirectory("out-map"))
            ]);

        var exitCode = orchestrator.Run();

        Assert.Equal(1, exitCode);
    }

    private static ExportOrchestrator CreateOrchestrator(
        TestWorkspace workspace,
        Func<string, int?> detectRevitMajor,
        IReadOnlyList<RevitModel> models)
    {
        var exportConfig = workspace.CreateDirectory("export-config");
        var adminData = workspace.CreateDirectory("admin_data");

        var settings = TestAppSettingsFactory.Create(
            workspace.Root,
            dirExportConfig: exportConfig,
            dirAdminData: adminData,
            runRevit: false);

        var historyStore = new StubHistoryStore();

        return new ExportOrchestrator(
            settings,
            new StubManageWorkbookLoader(models),
            historyStore,
            new ExportDiagnosticsWriter(historyStore),
            new ExportModelSelectionService(new StubIfcFreshnessChecker()),
            new RevitBatchPlanBuilder(detectRevitMajor),
            new RevitBatchRunner(new StubRevitLauncher(), new TransferStore()),
            new OutputDirectoryPreparer());
    }

    private sealed class StubManageWorkbookLoader : IManageWorkbookLoader
    {
        private readonly IReadOnlyList<RevitModel> _models;

        public StubManageWorkbookLoader(IReadOnlyList<RevitModel> models)
        {
            _models = models;
        }

        public ManageWorkbookData Load(
            string manageXlsxPath,
            AppSettings stg,
            ProjectPaths paths)
        {
            return new ManageWorkbookData
            {
                Models = _models.ToList(),
                Ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                MTimeIssues = []
            };
        }
    }

    private sealed class StubHistoryStore : IHistoryStore
    {
        public IReadOnlyList<HistoryRow> ReadRows(string historyWorkbookPath, string sheetName)
            => Array.Empty<HistoryRow>();

        public void Save(
            string historyWorkbookPath,
            string sheetName,
            IReadOnlyList<HistoryRow> rows)
        {
        }
    }

    private sealed class StubIfcFreshnessChecker : IIfcFreshnessChecker
    {
        public bool IsIfcUpToDateMapping(RevitModel model) => false;

        public bool IsIfcUpToDateNoMap(RevitModel model) => false;
    }

    private sealed class StubRevitLauncher : IRevitLauncher
    {
        public bool RunAndWait(
            int revitMajor,
            string taskFilePath,
            string dirAdminData,
            string iniPath,
            string runId,
            int timeoutMinutes)
            => true;
    }
}
