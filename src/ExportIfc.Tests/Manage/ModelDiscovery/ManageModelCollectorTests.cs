using ExportIfc.Manage;
using ExportIfc.Tests.TestInfrastructure;

using Xunit;

namespace ExportIfc.Tests.Manage.ModelDiscovery;

/// <summary>
/// Проверяет сбор RVT-моделей из директории без захвата служебных файлов Revit.
/// </summary>
public sealed class ManageModelCollectorTests
{
    [Fact]
    public void Collect_ReturnsOnlyUserModels_InDeterministicOrder()
    {
        using var workspace = TestWorkspace.Create();

        var modelDir = workspace.CreateDirectory("models");
        File.WriteAllText(System.IO.Path.Combine(modelDir, "B.rvt"), "b");
        File.WriteAllText(System.IO.Path.Combine(modelDir, "A.rvt"), "a");
        File.WriteAllText(System.IO.Path.Combine(modelDir, "Campus.A.rvt"), "campus");
        File.WriteAllText(System.IO.Path.Combine(modelDir, "Project.v2.rvt"), "project");
        File.WriteAllText(System.IO.Path.Combine(modelDir, "__Model2.0001.rvt"), "backup");
        File.WriteAllText(System.IO.Path.Combine(modelDir, "__Model2.0001.IFC.rvt"), "ifc-backup");
        File.WriteAllText(System.IO.Path.Combine(modelDir, "Linked.ifc.rvt"), "linked-ifc");
        File.WriteAllText(System.IO.Path.Combine(modelDir, "~$temp.rvt"), "temp");
        File.WriteAllText(System.IO.Path.Combine(modelDir, "note.txt"), "txt");

        var rowData = new ManagePathRowData
        {
            RowKey = "row-2",
            RvtDir = modelDir,
            OutputDirMapping = workspace.GetPath("out-map"),
            MappingJson = workspace.GetPath("mapping.json"),
            IfcClassMappingFile = workspace.GetPath("family-map.txt"),
            OutputDirNoMap = null,
            NoMapJson = null
        };

        var issues = new List<string>();

        var models = ManageModelCollector.Collect(rowData, issues);

        Assert.Empty(issues);
        Assert.Equal(4, models.Count);
        Assert.Equal(System.IO.Path.Combine(modelDir, "A.rvt"), models[0].RvtPath);
        Assert.Equal(System.IO.Path.Combine(modelDir, "B.rvt"), models[1].RvtPath);
        Assert.Equal(System.IO.Path.Combine(modelDir, "Campus.A.rvt"), models[2].RvtPath);
        Assert.Equal(System.IO.Path.Combine(modelDir, "Project.v2.rvt"), models[3].RvtPath);
    }
}
