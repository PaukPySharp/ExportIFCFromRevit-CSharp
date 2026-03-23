using ExportIfc.Config;
using ExportIfc.Tests.TestInfrastructure;
using ExportIfc.Settings;

using Xunit;

namespace ExportIfc.Tests.Settings;

/// <summary>
/// Проверяет сборку runtime-путей из <see cref="AppSettings"/>.
/// </summary>
public sealed class ProjectPathsTests
{
    [Fact]
    public void Build_DerivesLogAndHistoryDirectories_FromEffectiveAdminData()
    {
        using var workspace = TestWorkspace.Create();

        var exportConfig = workspace.CreateDirectory("export-config");
        var adminData = workspace.CreateDirectory("admin-data");

        var settings = TestAppSettingsFactory.Create(
            workspace.Root,
            dirExportConfig: exportConfig,
            dirAdminData: adminData,
            sheetIgnore: "Ignore");

        var paths = ProjectPaths.Build(settings);

        Assert.Equal(exportConfig, paths.DirExportConfig);
        Assert.Equal(adminData, paths.DirAdminData);
        Assert.Equal(System.IO.Path.Combine(adminData, LogDirectoryNames.Logs), paths.DirLogs);
        Assert.Equal(System.IO.Path.Combine(adminData, LogDirectoryNames.Logs, LogDirectoryNames.Tech), paths.DirTechLogs);
        Assert.Equal(
            System.IO.Path.Combine(adminData, LogDirectoryNames.Logs, LogDirectoryNames.Tech, LogDirectoryNames.Console),
            ProjectDirectories.TechConsoleLogs(adminData));
        Assert.Equal(System.IO.Path.Combine(adminData, ProjectDirectoryNames.History), paths.DirHistory);
    }

    [Fact]
    public void Build_Throws_WhenAdminDataDirectoryDoesNotExist()
    {
        using var workspace = TestWorkspace.Create();

        var exportConfig = workspace.CreateDirectory("export-config");
        var missingAdminData = workspace.GetPath("missing-admin-data");

        var settings = TestAppSettingsFactory.Create(
            workspace.Root,
            dirExportConfig: exportConfig,
            dirAdminData: missingAdminData,
            sheetIgnore: "Ignore");

        Assert.Throws<DirectoryNotFoundException>(() => ProjectPaths.Build(settings));
    }
}
