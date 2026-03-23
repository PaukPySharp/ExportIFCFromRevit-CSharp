using ExportIfc.Logging;
using ExportIfc.Manage;
using ExportIfc.Tests.TestInfrastructure;

using Xunit;

namespace ExportIfc.Tests.Manage.PathRows;

/// <summary>
/// Проверяет парсинг строк листа Path и валидацию связанных no-map полей.
/// </summary>
public sealed class ManagePathRowParserTests
{
    [Fact]
    public void TryParse_ReturnsParsedRow_WhenRowIsValid()
    {
        using var workspace = TestWorkspace.Create();

        var rvtDir = workspace.CreateDirectory("models");
        var exportConfig = workspace.CreateDirectory("export-config");
        var mappingDir = workspace.CreateDirectory("mapping-json");
        var outMap = workspace.GetPath("out-map");
        var outNoMap = workspace.GetPath("out-nomap");

        Directory.CreateDirectory(System.IO.Path.Combine(exportConfig, "00_Common"));
        Directory.CreateDirectory(System.IO.Path.Combine(exportConfig, "01_Layers"));

        File.WriteAllText(System.IO.Path.Combine(mappingDir, "Export_Settings.json"), "{}");
        File.WriteAllText(System.IO.Path.Combine(exportConfig, "00_Common", "NoMap.json"), "{}");
        File.WriteAllText(System.IO.Path.Combine(exportConfig, "01_Layers", "FamilyMap.txt"), "text");

        var settings = TestAppSettingsFactory.Create(
            workspace.Root,
            dirExportConfig: exportConfig,
            enableUnmappedExport: true);

        var sut = new ManagePathRowParser(settings, exportConfig, new ConsoleLogger("test"));

        var result = sut.TryParse(
            2,
            new ManagePathRowValues(
                rvtDir,
                outMap,
                mappingDir,
                "FamilyMap",
                outNoMap,
                "NoMap"));

        Assert.NotNull(result);
        Assert.Equal(rvtDir, result!.RvtDir);
        Assert.Equal(outMap, result.OutputDirMapping);
        Assert.Equal(outNoMap, result.OutputDirNoMap);
        Assert.EndsWith(System.IO.Path.Combine("mapping-json", "Export_Settings.json"), result.MappingJson);
        Assert.EndsWith(System.IO.Path.Combine("00_Common", "NoMap.json"), result.NoMapJson);
        Assert.EndsWith(System.IO.Path.Combine("01_Layers", "FamilyMap.txt"), result.IfcClassMappingFile);
    }

    [Fact]
    public void TryParse_ReturnsNull_WhenOnlyOneNoMapFieldIsFilled()
    {
        using var workspace = TestWorkspace.Create();

        var rvtDir = workspace.CreateDirectory("models");
        var exportConfig = workspace.CreateDirectory("export-config");
        var mappingDir = workspace.CreateDirectory("mapping-json");

        Directory.CreateDirectory(System.IO.Path.Combine(exportConfig, "01_Layers"));

        File.WriteAllText(System.IO.Path.Combine(mappingDir, "Export_Settings.json"), "{}");
        File.WriteAllText(System.IO.Path.Combine(exportConfig, "01_Layers", "FamilyMap.txt"), "text");

        var settings = TestAppSettingsFactory.Create(
            workspace.Root,
            dirExportConfig: exportConfig,
            enableUnmappedExport: true);

        var sut = new ManagePathRowParser(settings, exportConfig, new ConsoleLogger("test"));

        var result = sut.TryParse(
            2,
            new ManagePathRowValues(
                rvtDir,
                workspace.GetPath("out-map"),
                mappingDir,
                "FamilyMap",
                workspace.GetPath("out-nomap"),
                string.Empty));

        Assert.Null(result);
    }

    [Fact]
    public void TryParse_Throws_WhenFamilyMapContainsDirectorySegments()
    {
        using var workspace = TestWorkspace.Create();

        var rvtDir = workspace.CreateDirectory("models");
        var exportConfig = workspace.CreateDirectory("export-config");
        var mappingDir = workspace.CreateDirectory("mapping-json");

        Directory.CreateDirectory(System.IO.Path.Combine(exportConfig, "01_Layers"));

        File.WriteAllText(System.IO.Path.Combine(mappingDir, "Export_Settings.json"), "{}");
        File.WriteAllText(System.IO.Path.Combine(exportConfig, "01_Layers", "FamilyMap.txt"), "text");

        var settings = TestAppSettingsFactory.Create(
            workspace.Root,
            dirExportConfig: exportConfig,
            enableUnmappedExport: false);

        var sut = new ManagePathRowParser(settings, exportConfig, new ConsoleLogger("test"));

        Assert.Throws<InvalidDataException>(() => sut.TryParse(
            2,
            new ManagePathRowValues(
                rvtDir,
                workspace.GetPath("out-map"),
                mappingDir,
                @"subdir\FamilyMap",
                string.Empty,
                string.Empty)));
    }
}
