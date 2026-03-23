using System.Text;

using ExportIfc.Config;
using ExportIfc.Tests.TestInfrastructure;
using ExportIfc.Settings.Loading;

using Xunit;

namespace ExportIfc.Tests.Settings;

/// <summary>
/// Проверяет выбор эффективного <c>DirAdminData</c> в prod- и non-prod режиме.
/// </summary>
public sealed class AppSettingsLoaderTests
{
    [Fact]
    public void Load_UsesLocalAdminData_WhenProdModeIsDisabled()
    {
        using var workspace = TestWorkspace.Create();

        var settingsDir = workspace.CreateDirectory(ProjectDirectoryNames.Settings);
        var exportConfig = workspace.CreateDirectory("export-config");
        var localAdminData = workspace.CreateDirectory(ProjectDirectoryNames.AdminData);

        var iniPath = System.IO.Path.Combine(settingsDir, ProjectFileNames.SettingsIni);
        File.WriteAllText(
            iniPath,
            TestIniBuilder.BuildAppSettings(exportConfig, adminData: null, isProdMode: false),
            Encoding.UTF8);

        var settings = AppSettingsLoader.Load(iniPath);

        Assert.False(settings.IsProdMode);
        Assert.Equal(localAdminData, settings.DirAdminData);
    }

    [Fact]
    public void Load_UsesConfiguredAdminData_WhenProdModeIsEnabled()
    {
        using var workspace = TestWorkspace.Create();

        var settingsDir = workspace.CreateDirectory(ProjectDirectoryNames.Settings);
        var exportConfig = workspace.CreateDirectory("export-config");
        var configuredAdminData = workspace.CreateDirectory("configured-admin-data");

        var iniPath = System.IO.Path.Combine(settingsDir, ProjectFileNames.SettingsIni);
        File.WriteAllText(
            iniPath,
            TestIniBuilder.BuildAppSettings(exportConfig, configuredAdminData, isProdMode: true),
            Encoding.UTF8);

        var settings = AppSettingsLoader.Load(iniPath);

        Assert.True(settings.IsProdMode);
        Assert.Equal(configuredAdminData, settings.DirAdminData);
    }
}
