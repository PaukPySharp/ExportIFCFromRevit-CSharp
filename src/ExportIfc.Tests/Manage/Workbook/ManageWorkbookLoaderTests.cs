using ClosedXML.Excel;

using ExportIfc.Config;
using ExportIfc.Manage;
using ExportIfc.Tests.TestInfrastructure;
using ExportIfc.Settings;

using Xunit;

namespace ExportIfc.Tests.Manage.Workbook;

/// <summary>
/// Проверяет чтение <c>manage.xlsx</c> и граничные правила листа Path.
/// </summary>
public sealed class ManageWorkbookLoaderTests
{
    [Fact]
    public void Load_StopsReadingPathSheet_OnFirstBlankRow()
    {
        using var workspace = TestWorkspace.Create();

        var settings = CreateSettings(workspace);
        var paths = ProjectPaths.Build(settings);
        var workbookPath = workspace.GetPath("manage.xlsx");
        var exportConfig = settings.DirExportConfig;
        var modelsA = workspace.CreateDirectory("models-a");
        var modelsB = workspace.CreateDirectory("models-b");
        var mappingJsonDir = workspace.CreateDirectory("mapping-json");

        File.WriteAllText(System.IO.Path.Combine(modelsA, "A.rvt"), "a");
        File.WriteAllText(System.IO.Path.Combine(modelsB, "B.rvt"), "b");
        File.WriteAllText(System.IO.Path.Combine(mappingJsonDir, "Export_Settings.json"), "{}");
        File.WriteAllText(System.IO.Path.Combine(exportConfig, "01_Layers", "FamilyMap.txt"), "text");

        using (var workbook = new XLWorkbook())
        {
            var pathSheet = workbook.Worksheets.Add("Path");
            pathSheet.Cell(1, 1).Value = "rvt";
            pathSheet.Cell(1, 2).Value = "outMap";
            pathSheet.Cell(1, 3).Value = "mapDir";
            pathSheet.Cell(1, 4).Value = "familyMap";
            pathSheet.Cell(1, 5).Value = "outNoMap";
            pathSheet.Cell(1, 6).Value = "noMapName";

            pathSheet.Cell(2, 1).Value = modelsA;
            pathSheet.Cell(2, 2).Value = workspace.GetPath("out-map-a");
            pathSheet.Cell(2, 3).Value = mappingJsonDir;
            pathSheet.Cell(2, 4).Value = "FamilyMap";

            // Пустая строка Path завершает чтение листа и отсекает хвост ниже неё.

            pathSheet.Cell(4, 1).Value = modelsB;
            pathSheet.Cell(4, 2).Value = workspace.GetPath("out-map-b");
            pathSheet.Cell(4, 3).Value = mappingJsonDir;
            pathSheet.Cell(4, 4).Value = "FamilyMap";

            workbook.Worksheets.Add("IgnoreList");
            workbook.SaveAs(workbookPath);
        }

        var sut = new ManageWorkbookLoader();
        var result = sut.Load(workbookPath, settings, paths);

        Assert.Single(result.Models);
        Assert.Contains(result.Models, model => model.RvtPath.EndsWith("A.rvt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Models, model => model.RvtPath.EndsWith("B.rvt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_Throws_WhenMandatoryPathSheetIsMissing()
    {
        using var workspace = TestWorkspace.Create();

        var settings = CreateSettings(workspace);
        var paths = ProjectPaths.Build(settings);
        var workbookPath = workspace.GetPath("manage.xlsx");

        using (var workbook = new XLWorkbook())
        {
            workbook.Worksheets.Add("IgnoreList");
            workbook.SaveAs(workbookPath);
        }

        var sut = new ManageWorkbookLoader();

        var ex = Assert.Throws<InvalidDataException>(() => sut.Load(workbookPath, settings, paths));

        Assert.Contains("Path", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(workbookPath, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Создаёт минимальный набор настроек, достаточный для загрузки <c>manage.xlsx</c>.
    /// </summary>
    /// <param name="workspace">Рабочее пространство теста.</param>
    /// <returns>Подготовленные настройки приложения.</returns>
    private static AppSettings CreateSettings(TestWorkspace workspace)
    {
        var exportConfig = workspace.CreateDirectory("export-config");
        var adminData = workspace.CreateDirectory("admin_data");
        Directory.CreateDirectory(System.IO.Path.Combine(exportConfig, "01_Layers"));

        return TestAppSettingsFactory.Create(
            workspace.Root,
            dirExportConfig: exportConfig,
            dirAdminData: adminData);
    }
}
