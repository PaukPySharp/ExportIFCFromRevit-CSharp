using ExportIfc.Tests.TestInfrastructure;
using ExportIfc.Validation;

using Xunit;

namespace ExportIfc.Tests.Validation;

/// <summary>
/// Проверяет базовые правила актуальности IFC для mapping- и no-map маршрутов.
/// </summary>
public sealed class IfcFreshnessCheckerTests
{
    [Fact]
    public void IsIfcUpToDateMapping_ReturnsTrue_WhenIfcIsNotOlderThanModel()
    {
        using var workspace = TestWorkspace.Create();

        var outputDir = workspace.CreateDirectory("ifc-map");
        var modelTime = new DateTime(2026, 3, 8, 10, 0, 0);
        var model = TestModelFactory.Create(
            workspace.GetPath("Model.rvt"),
            modelTime,
            outputDir,
            null);

        var ifcPath = model.ExpectedIfcPathMapping()!;
        File.WriteAllText(ifcPath, "ifc");
        File.SetLastWriteTime(ifcPath, modelTime);

        var sut = new IfcFreshnessChecker();

        Assert.True(sut.IsIfcUpToDateMapping(model));
    }

    [Fact]
    public void IsIfcUpToDateMapping_ReturnsFalse_WhenIfcIsMissing()
    {
        using var workspace = TestWorkspace.Create();

        var model = TestModelFactory.Create(
            workspace.GetPath("Model.rvt"),
            new DateTime(2026, 3, 8, 10, 0, 0),
            workspace.CreateDirectory("ifc-map"),
            null);

        var sut = new IfcFreshnessChecker();

        Assert.False(sut.IsIfcUpToDateMapping(model));
    }

    [Fact]
    public void IsIfcUpToDateNoMap_ReturnsTrue_WhenDirectionIsNotRequired()
    {
        var model = TestModelFactory.Create(
            @"C:\Models\Model.rvt",
            new DateTime(2026, 3, 8, 10, 0, 0),
            @"C:\Out\Map",
            null);

        var sut = new IfcFreshnessChecker();

        Assert.True(sut.IsIfcUpToDateNoMap(model));
    }
}
