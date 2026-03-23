using ExportIfc.Export.Planning;
using ExportIfc.History;
using ExportIfc.Tests.TestInfrastructure;

using Xunit;

namespace ExportIfc.Tests.Export.Planning;

/// <summary>
/// Проверяет построение batch-плана по версиям Revit и обновление истории по итогам планирования.
/// </summary>
public sealed class RevitBatchPlanBuilderTests
{
    [Fact]
    public void Build_GroupsModelsByLaunchVersion_AndMapsOlderVersionsToMinimumSupported()
    {
        var dt = new DateTime(2026, 3, 8, 10, 0, 0);

        var model2021 = TestModelFactory.Create(@"C:\Models\A.rvt", dt);
        var model2022 = TestModelFactory.Create(@"C:\Models\B.rvt", dt);
        var model2024 = TestModelFactory.Create(@"C:\Models\C.rvt", dt);

        var versions = new Dictionary<string, int?>
        {
            [model2021.RvtPath] = 2021,
            [model2022.RvtPath] = 2022,
            [model2024.RvtPath] = 2024
        };

        var sut = new RevitBatchPlanBuilder(path => versions[path]);
        var history = HistoryManager.FromRows(Array.Empty<HistoryRow>());

        var plan = sut.Build(
            [model2024, model2021, model2022],
            [2022, 2024],
            history);

        Assert.Equal(2, plan.Batches.Count);

        Assert.Equal(2022, plan.Batches[0].RevitMajor);
        Assert.Equal(
            [model2021.RvtPath, model2022.RvtPath],
            plan.Batches[0].Models.Select(x => x.RvtPath).ToArray());

        Assert.Equal(2024, plan.Batches[1].RevitMajor);
        Assert.Equal(model2024.RvtPath, plan.Batches[1].Models[0].RvtPath);

        Assert.True(history.IsUpToDate(model2021));
        Assert.True(history.IsUpToDate(model2022));
        Assert.True(history.IsUpToDate(model2024));
    }

    [Fact]
    public void Build_MapsGapVersionsToNextAvailableSupportedRevit()
    {
        var dt = new DateTime(2026, 3, 8, 10, 0, 0);

        var model2023 = TestModelFactory.Create(@"C:\Models\A.rvt", dt);
        var model2025 = TestModelFactory.Create(@"C:\Models\B.rvt", dt);
        var model2026 = TestModelFactory.Create(@"C:\Models\C.rvt", dt);

        var versions = new Dictionary<string, int?>
        {
            [model2023.RvtPath] = 2023,
            [model2025.RvtPath] = 2025,
            [model2026.RvtPath] = 2026
        };

        var sut = new RevitBatchPlanBuilder(path => versions[path]);
        var history = HistoryManager.FromRows(Array.Empty<HistoryRow>());

        var plan = sut.Build(
            [model2026, model2023, model2025],
            [2022, 2024, 2026],
            history);

        Assert.Equal(2, plan.Batches.Count);

        Assert.Equal(2024, plan.Batches[0].RevitMajor);
        Assert.Equal(model2023.RvtPath, plan.Batches[0].Models[0].RvtPath);

        Assert.Equal(2026, plan.Batches[1].RevitMajor);
        Assert.Equal(
            [model2025.RvtPath, model2026.RvtPath],
            plan.Batches[1].Models.Select(x => x.RvtPath).ToArray());

        Assert.Empty(plan.VersionTooNew);
        Assert.Empty(plan.VersionNotFound);
    }

    [Fact]
    public void Build_CollectsDiagnostics_ForUnknownAndTooNewVersions()
    {
        var dt = new DateTime(2026, 3, 8, 10, 0, 0);

        var unknownModel = TestModelFactory.Create(@"C:\Models\Unknown.rvt", dt);
        var tooNewModel = TestModelFactory.Create(@"C:\Models\TooNew.rvt", dt);
        var validModel = TestModelFactory.Create(@"C:\Models\Valid.rvt", dt);

        var versions = new Dictionary<string, int?>
        {
            [unknownModel.RvtPath] = null,
            [tooNewModel.RvtPath] = 2027,
            [validModel.RvtPath] = 2024
        };

        var sut = new RevitBatchPlanBuilder(path => versions[path]);
        var history = HistoryManager.FromRows(Array.Empty<HistoryRow>());

        var plan = sut.Build(
            [unknownModel, tooNewModel, validModel],
            [2022, 2024],
            history);

        Assert.Single(plan.VersionNotFound);
        Assert.Equal(unknownModel.RvtPath, plan.VersionNotFound[0]);

        Assert.Single(plan.VersionTooNew);
        Assert.Contains(tooNewModel.RvtPath, plan.VersionTooNew[0]);
        Assert.Contains("2022, 2024", plan.VersionTooNew[0], StringComparison.Ordinal);

        Assert.Single(plan.Batches);
        Assert.Equal(2024, plan.Batches[0].RevitMajor);
        Assert.Equal(validModel.RvtPath, plan.Batches[0].Models[0].RvtPath);

        // Слишком новые модели уже диагностированы на этом прогоне.
        // History помечает их обработанными и не дублирует одно и то же состояние.
        Assert.False(history.IsUpToDate(unknownModel));
        Assert.True(history.IsUpToDate(tooNewModel));
        Assert.True(history.IsUpToDate(validModel));
    }
}
