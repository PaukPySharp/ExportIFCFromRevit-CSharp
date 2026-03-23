using ExportIfc.Config;
using ExportIfc.RevitAddin.Batch.Context;
using ExportIfc.Tests.TestInfrastructure;
using ExportIfc.Transfer;

using Xunit;

namespace ExportIfc.Tests.Batch.Context;

/// <summary>
/// Проверяет чтение runtime-контекста пакетного запуска add-in из переменных окружения.
/// </summary>
public sealed class BatchRunContextReaderTests
{
    [Fact]
    public void TryRead_ReturnsContext_ForValidEnvironment()
    {
        using var workspace = TestWorkspace.Create();

        var exportConfig = workspace.CreateDirectory("export-config");
        var adminData = workspace.CreateDirectory("admin_data");
        var iniPath = workspace.GetPath(ProjectFileNames.SettingsIni);
        var taskFilePath = workspace.GetPath("Task2024.txt");
        var runId = "run-20260322_150000_123";

        File.WriteAllText(iniPath, TestIniBuilder.BuildAppSettings(exportConfig, null, isProdMode: false));
        File.WriteAllText(taskFilePath, @"C:\Models\Model.rvt");

        using var env = new EnvironmentVariableScope(
            (EnvironmentVariableNames.AdminData, adminData),
            (EnvironmentVariableNames.TaskFile, taskFilePath),
            (EnvironmentVariableNames.SettingsIni, iniPath),
            (EnvironmentVariableNames.RunId, runId),
            (EnvironmentVariableNames.RevitMajor, "2024"));

        var sut = new BatchRunContextReader();

        var ok = sut.TryRead(out var context);

        Assert.True(ok);
        Assert.NotNull(context);
        Assert.Equal(adminData, context!.DirAdminData);
        Assert.Equal(taskFilePath, context.TaskFilePath);
        Assert.Equal(iniPath, context.IniPath);
        Assert.Equal(runId, context.RunId);
        Assert.Equal(2024, context.RevitMajor);
        Assert.Equal(ProjectFiles.TmpJson(adminData), context.TmpJsonPath);
        Assert.Equal("Navisworks", context.ExportViewName);
        Assert.False(context.EnableUnmappedExport);
    }

    [Fact]
    public void TryRead_ReturnsFalse_AndWritesFailedStatus_WhenRevitMajorIsInvalid()
    {
        using var workspace = TestWorkspace.Create();

        var exportConfig = workspace.CreateDirectory("export-config");
        var adminData = workspace.CreateDirectory("admin_data");
        var iniPath = workspace.GetPath(ProjectFileNames.SettingsIni);
        var taskFilePath = workspace.GetPath("Task2024.txt");
        var runId = "run-20260322_150500_123";

        File.WriteAllText(iniPath, TestIniBuilder.BuildAppSettings(exportConfig, null, isProdMode: false));
        File.WriteAllText(taskFilePath, @"C:\Models\Model.rvt");

        using var env = new EnvironmentVariableScope(
            (EnvironmentVariableNames.AdminData, adminData),
            (EnvironmentVariableNames.TaskFile, taskFilePath),
            (EnvironmentVariableNames.SettingsIni, iniPath),
            (EnvironmentVariableNames.RunId, runId),
            (EnvironmentVariableNames.RevitMajor, "0"));

        var sut = new BatchRunContextReader();

        var ok = sut.TryRead(out var context);

        Assert.False(ok);
        Assert.Null(context);
        Assert.Equal(
            BatchRunStatuses.Failed,
            AddinRunStatusReader.TryReadStatus(
                ProjectFiles.AddinStatusFile(adminData),
                runId,
                Path.GetFileName(taskFilePath)));
    }

    [Fact]
    public void TryRead_ReturnsFalse_AndWritesFailedStatus_WhenRunIdIsMissing()
    {
        using var workspace = TestWorkspace.Create();

        var exportConfig = workspace.CreateDirectory("export-config");
        var adminData = workspace.CreateDirectory("admin_data");
        var iniPath = workspace.GetPath(ProjectFileNames.SettingsIni);
        var taskFilePath = workspace.GetPath("Task2024.txt");

        File.WriteAllText(iniPath, TestIniBuilder.BuildAppSettings(exportConfig, null, isProdMode: false));
        File.WriteAllText(taskFilePath, @"C:\Models\Model.rvt");

        using var env = new EnvironmentVariableScope(
            (EnvironmentVariableNames.AdminData, adminData),
            (EnvironmentVariableNames.TaskFile, taskFilePath),
            (EnvironmentVariableNames.SettingsIni, iniPath),
            (EnvironmentVariableNames.RunId, string.Empty),
            (EnvironmentVariableNames.RevitMajor, "2024"));

        var sut = new BatchRunContextReader();

        var ok = sut.TryRead(out var context);

        Assert.False(ok);
        Assert.Null(context);

        var statusLog = File.ReadAllText(ProjectFiles.AddinStatusFile(adminData));

        Assert.Contains(AddinLogSchema.RunIdPrefix + "<no-run-id>", statusLog);
        Assert.Contains(AddinLogSchema.StatusPrefix + BatchRunStatuses.Failed, statusLog);
    }
}
