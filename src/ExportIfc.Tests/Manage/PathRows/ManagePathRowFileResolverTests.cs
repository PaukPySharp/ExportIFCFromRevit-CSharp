using ExportIfc.Manage;
using ExportIfc.Tests.TestInfrastructure;

using Xunit;

namespace ExportIfc.Tests.Manage.PathRows;

/// <summary>
/// Проверяет резолвинг имён конфигурационных файлов в проектные каталоги export-config.
/// </summary>
public sealed class ManagePathRowFileResolverTests
{
    [Fact]
    public void ResolveIfcClassMappingFile_BuildsPathInLayersDirectory_AndAppendsTxtExtension()
    {
        using var workspace = TestWorkspace.Create();

        var exportConfig = workspace.CreateDirectory("export-config");
        var layersDir = System.IO.Path.Combine(exportConfig, "01_Layers");
        Directory.CreateDirectory(layersDir);
        File.WriteAllText(System.IO.Path.Combine(layersDir, "FamilyMap.txt"), "text");

        var settings = TestAppSettingsFactory.Create(workspace.Root, dirExportConfig: exportConfig);
        var sut = new ManagePathRowFileResolver(settings, exportConfig);

        var fullPath = sut.ResolveIfcClassMappingFile(2, "FamilyMap");

        Assert.Equal(System.IO.Path.Combine(layersDir, "FamilyMap.txt"), fullPath);
    }

    [Fact]
    public void ResolveNoMapJson_BuildsPathInCommonDirectory_AndAppendsJsonExtension()
    {
        using var workspace = TestWorkspace.Create();

        var exportConfig = workspace.CreateDirectory("export-config");
        var commonDir = System.IO.Path.Combine(exportConfig, "00_Common");
        Directory.CreateDirectory(commonDir);
        File.WriteAllText(System.IO.Path.Combine(commonDir, "NoMap.json"), "{}");

        var settings = TestAppSettingsFactory.Create(workspace.Root, dirExportConfig: exportConfig);
        var sut = new ManagePathRowFileResolver(settings, exportConfig);

        var fullPath = sut.ResolveNoMapJson(3, "NoMap");

        Assert.Equal(System.IO.Path.Combine(commonDir, "NoMap.json"), fullPath);
    }
}
