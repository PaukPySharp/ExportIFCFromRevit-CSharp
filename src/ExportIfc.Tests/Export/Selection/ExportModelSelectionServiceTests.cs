using ExportIfc.Export.Selection;
using ExportIfc.History;
using ExportIfc.Logging;
using ExportIfc.Tests.TestInfrastructure;
using ExportIfc.Validation;

using Xunit;

namespace ExportIfc.Tests.Export.Selection;

/// <summary>
/// Проверяет отбор моделей на выгрузку с учётом history и актуальности IFC.
/// </summary>
public sealed class ExportModelSelectionServiceTests
{
    [Fact]
    public void SelectModelsToExport_SkipsFullyActualModel_AndKeepsStaleModel()
    {
        using var workspace = TestWorkspace.Create();

        var freshMapDir = workspace.CreateDirectory("fresh-map");
        var freshNoMapDir = workspace.CreateDirectory("fresh-nomap");
        var staleMapDir = workspace.CreateDirectory("stale-map");
        var staleNoMapDir = workspace.CreateDirectory("stale-nomap");

        var dt = new DateTime(2026, 3, 8, 10, 0, 0);

        var freshModel = TestModelFactory.Create(
            workspace.GetPath("Fresh.rvt"),
            dt,
            freshMapDir,
            freshNoMapDir);

        var staleModel = TestModelFactory.Create(
            workspace.GetPath("Stale.rvt"),
            dt,
            staleMapDir,
            staleNoMapDir);

        var freshMapIfc = freshModel.ExpectedIfcPathMapping()!;
        File.WriteAllText(freshMapIfc, "ifc");
        File.SetLastWriteTime(freshMapIfc, dt.AddMinutes(1));

        var freshNoMapIfc = freshModel.ExpectedIfcPathNoMap()!;
        File.WriteAllText(freshNoMapIfc, "ifc");
        File.SetLastWriteTime(freshNoMapIfc, dt.AddMinutes(1));

        var history = HistoryManager.FromRows(
            [
                new HistoryRow(freshModel.RvtPath, dt)
            ]);

        var sut = new ExportModelSelectionService(new IfcFreshnessChecker());
        var log = new ConsoleLogger("test");

        var result = sut.SelectModelsToExport(
            [freshModel, staleModel],
            history,
            log);

        Assert.Single(result.ModelsToExport);
        Assert.Equal(staleModel.RvtPath, result.ModelsToExport[0].RvtPath);
        Assert.Equal(1, result.SkippedAsActual);

        // Сервис не должен мутировать исходные модели: в batch-этап уходит отдельная проекция.
        Assert.Equal(freshMapDir, freshModel.OutputDirMapping);
        Assert.Equal(freshNoMapDir, freshModel.OutputDirNoMap);
        Assert.Equal(staleMapDir, staleModel.OutputDirMapping);
        Assert.Equal(staleNoMapDir, staleModel.OutputDirNoMap);

        var exportModel = result.ModelsToExport[0];
        Assert.Equal(staleMapDir, exportModel.OutputDirMapping);
        Assert.Equal(staleNoMapDir, exportModel.OutputDirNoMap);
    }

    [Fact]
    public void SelectModelsToExport_ForcesFullExport_WhenIfcIsFreshButHistoryIsStale()
    {
        using var workspace = TestWorkspace.Create();

        var outputMapDir = workspace.CreateDirectory("map");
        var outputNoMapDir = workspace.CreateDirectory("nomap");

        var modelMtime = new DateTime(2026, 3, 20, 20, 29, 0);
        var historyMtime = modelMtime.AddMinutes(-4);

        var model = TestModelFactory.Create(
            workspace.GetPath("ForcedByHistory.rvt"),
            modelMtime,
            outputMapDir,
            outputNoMapDir);

        var mapIfc = model.ExpectedIfcPathMapping()!;
        File.WriteAllText(mapIfc, "ifc");
        File.SetLastWriteTime(mapIfc, modelMtime.AddHours(1));

        var noMapIfc = model.ExpectedIfcPathNoMap()!;
        File.WriteAllText(noMapIfc, "ifc");
        File.SetLastWriteTime(noMapIfc, modelMtime.AddHours(1));

        var history = HistoryManager.FromRows(
            [
                new HistoryRow(model.RvtPath, historyMtime)
            ]);

        var sut = new ExportModelSelectionService(new IfcFreshnessChecker());
        var log = new ConsoleLogger("test");

        var result = sut.SelectModelsToExport(
            [model],
            history,
            log);

        var exportModel = Assert.Single(result.ModelsToExport);
        Assert.Equal(0, result.SkippedAsActual);
        Assert.False(history.IsUpToDate(model));
        Assert.Same(model, exportModel);
        Assert.Equal(model.RvtPath, exportModel.RvtPath);
        Assert.Equal(outputMapDir, exportModel.OutputDirMapping);
        Assert.Equal(outputNoMapDir, exportModel.OutputDirNoMap);
        Assert.Equal(outputMapDir, model.OutputDirMapping);
        Assert.Equal(outputNoMapDir, model.OutputDirNoMap);
    }
}
