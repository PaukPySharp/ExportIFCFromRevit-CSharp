using ExportIfc.Config;
using ExportIfc.Tests.TestInfrastructure;
using ExportIfc.Settings.Loading;

using Xunit;

namespace ExportIfc.Tests.Settings;

/// <summary>
/// Проверяет приоритеты резолвинга <c>settings.ini</c> на старте приложения.
/// </summary>
public sealed class SettingsIniLocatorTests
{
    [Fact]
    public void ResolveStartupPath_UsesCommandLineArgument_First()
    {
        using var workspace = TestWorkspace.Create();

        var iniPath = workspace.GetPath(ProjectFileNames.SettingsIni);
        File.WriteAllText(iniPath, TestIniBuilder.BuildMinimal());

        var resolved = SettingsIniLocator.ResolveStartupPath([iniPath]);

        Assert.Equal(System.IO.Path.GetFullPath(iniPath), resolved);
    }

    [Fact]
    public void ResolveStartupPath_UsesEnvironmentVariable_WhenArgumentMissing()
    {
        using var workspace = TestWorkspace.Create();
        var oldValue = Environment.GetEnvironmentVariable(EnvironmentVariableNames.SettingsIni);

        try
        {
            var iniPath = workspace.GetPath(ProjectFileNames.SettingsIni);
            File.WriteAllText(iniPath, TestIniBuilder.BuildMinimal());
            Environment.SetEnvironmentVariable(EnvironmentVariableNames.SettingsIni, iniPath);

            var resolved = SettingsIniLocator.ResolveStartupPath(Array.Empty<string>());

            Assert.Equal(System.IO.Path.GetFullPath(iniPath), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableNames.SettingsIni, oldValue);
        }
    }
}
