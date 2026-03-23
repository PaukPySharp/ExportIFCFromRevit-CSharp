using ExportIfc.Config;
using ExportIfc.Revit;

using Xunit;

namespace ExportIfc.Tests.Revit.Launching;

/// <summary>
/// Проверяет runtime-контракт запуска внешнего Revit-процесса.
/// </summary>
public sealed class RevitProcessStartInfoFactoryTests
{
    [Fact]
    public void Create_BuildsStartInfo_WithExpectedRuntimeContract()
    {
        var startInfo = RevitProcessStartInfoFactory.Create(
            exePath: @"C:\Program Files\Autodesk\Revit 2024\Revit.exe",
            revitMajor: 2024,
            taskFilePath: @"C:\Work\Task2024.txt",
            dirAdminData: @"C:\Work\admin_data",
            iniPath: @"C:\Work\settings.ini",
            runId: "run-42");

        Assert.Equal(@"C:\Program Files\Autodesk\Revit 2024\Revit.exe", startInfo.FileName);
        Assert.Equal(RevitConstants.NoSplashArguments, startInfo.Arguments);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);

        Assert.Equal(EnvironmentVariableValues.AutorunEnabled, startInfo.Environment[EnvironmentVariableNames.Autorun]);
        Assert.Equal(@"C:\Work\admin_data", startInfo.Environment[EnvironmentVariableNames.AdminData]);
        Assert.Equal(@"C:\Work\Task2024.txt", startInfo.Environment[EnvironmentVariableNames.TaskFile]);
        Assert.Equal("2024", startInfo.Environment[EnvironmentVariableNames.RevitMajor]);
        Assert.Equal(@"C:\Work\settings.ini", startInfo.Environment[EnvironmentVariableNames.SettingsIni]);
        Assert.Equal("run-42", startInfo.Environment[EnvironmentVariableNames.RunId]);
    }
}
