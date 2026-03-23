using ExportIfc.Config;
using ExportIfc.RevitAddin.Batch.Context;
using ExportIfc.RevitAddin.Batch.Input;
using ExportIfc.Tests.TestInfrastructure;
using ExportIfc.Transfer;

using Xunit;

namespace ExportIfc.Tests.Batch.Input;

/// <summary>
/// Проверяет загрузку и fail-fast-сверку входных данных пакетного запуска add-in.
/// </summary>
public sealed class BatchRunInputLoaderTests
{
    [Fact]
    public void TryLoad_ReturnsInput_ForMatchingEnvelopeAndTaskFile()
    {
        using var workspace = TestWorkspace.Create();

        var adminData = workspace.CreateDirectory("admin_data");
        var iniPath = workspace.GetPath(ProjectFileNames.SettingsIni);
        var taskFilePath = workspace.GetPath("Task2024.txt");
        var tmpJsonPath = ProjectFiles.TmpJson(adminData);
        var runId = "run-20260322_160000_123";
        var revitMajor = 2024;

        var transferStore = new TransferStore();
        var model = TestModelFactory.Create(
            workspace.GetPath("Model.rvt"),
            new DateTime(2026, 3, 22, 16, 0, 0),
            workspace.CreateDirectory("out-map"),
            null);

        var envelope = transferStore.BuildEnvelope(revitMajor, runId, [model]);
        transferStore.WriteEnvelope(tmpJsonPath, envelope);
        transferStore.WriteTaskModels(taskFilePath, [model.RvtPath]);

        using var env = CreateBatchEnvironment(adminData, taskFilePath, iniPath, runId, revitMajor);

        var context = CreateContext(adminData, taskFilePath, iniPath, runId, revitMajor);
        var sut = new BatchRunInputLoader(transferStore);

        var ok = sut.TryLoad(context, out var input);

        Assert.True(ok);
        Assert.NotNull(input);
        Assert.Equal(runId, input!.Envelope.RunId);
        Assert.Equal(revitMajor, input.Envelope.RevitMajor);
        Assert.Single(input.Items);
        Assert.Equal(model.RvtPath, input.Items[0].ModelPath);
        Assert.Equal([model.RvtPath], input.TaskModels);
    }

    [Fact]
    public void TryLoad_ReturnsFalse_AndWritesFailedStatus_WhenRunIdMismatches()
    {
        using var workspace = TestWorkspace.Create();

        var adminData = workspace.CreateDirectory("admin_data");
        var iniPath = workspace.GetPath(ProjectFileNames.SettingsIni);
        var taskFilePath = workspace.GetPath("Task2024.txt");
        var tmpJsonPath = ProjectFiles.TmpJson(adminData);
        var runId = "run-20260322_160500_123";
        var revitMajor = 2024;

        var transferStore = new TransferStore();
        var model = TestModelFactory.Create(
            workspace.GetPath("Model.rvt"),
            new DateTime(2026, 3, 22, 16, 5, 0),
            workspace.CreateDirectory("out-map"),
            null);

        var envelope = transferStore.BuildEnvelope(revitMajor, "run-other", [model]);
        transferStore.WriteEnvelope(tmpJsonPath, envelope);
        transferStore.WriteTaskModels(taskFilePath, [model.RvtPath]);

        using var env = CreateBatchEnvironment(adminData, taskFilePath, iniPath, runId, revitMajor);

        var context = CreateContext(adminData, taskFilePath, iniPath, runId, revitMajor);
        var sut = new BatchRunInputLoader(transferStore);

        var ok = sut.TryLoad(context, out var input);

        Assert.False(ok);
        Assert.Null(input);
        Assert.Equal(
            BatchRunStatuses.Failed,
            AddinRunStatusReader.TryReadStatus(
                ProjectFiles.AddinStatusFile(adminData),
                runId,
                Path.GetFileName(taskFilePath)));
    }

    [Fact]
    public void TryLoad_ReturnsFalse_AndWritesFailedStatus_WhenRevitMajorMismatches()
    {
        using var workspace = TestWorkspace.Create();

        var adminData = workspace.CreateDirectory("admin_data");
        var iniPath = workspace.GetPath(ProjectFileNames.SettingsIni);
        var taskFilePath = workspace.GetPath("Task2024.txt");
        var tmpJsonPath = ProjectFiles.TmpJson(adminData);
        var runId = "run-20260322_161000_123";

        var transferStore = new TransferStore();
        var model = TestModelFactory.Create(
            workspace.GetPath("Model.rvt"),
            new DateTime(2026, 3, 22, 16, 10, 0),
            workspace.CreateDirectory("out-map"),
            null);

        var envelope = transferStore.BuildEnvelope(2025, runId, [model]);
        transferStore.WriteEnvelope(tmpJsonPath, envelope);
        transferStore.WriteTaskModels(taskFilePath, [model.RvtPath]);

        using var env = CreateBatchEnvironment(adminData, taskFilePath, iniPath, runId, 2024);

        var context = CreateContext(adminData, taskFilePath, iniPath, runId, 2024);
        var sut = new BatchRunInputLoader(transferStore);

        var ok = sut.TryLoad(context, out var input);

        Assert.False(ok);
        Assert.Null(input);
        Assert.Equal(
            BatchRunStatuses.Failed,
            AddinRunStatusReader.TryReadStatus(
                ProjectFiles.AddinStatusFile(adminData),
                runId,
                Path.GetFileName(taskFilePath)));
    }

    [Fact]
    public void TryLoad_ReturnsFalse_AndWritesFailedStatus_WhenTaskFileMismatchesEnvelope()
    {
        using var workspace = TestWorkspace.Create();

        var adminData = workspace.CreateDirectory("admin_data");
        var iniPath = workspace.GetPath(ProjectFileNames.SettingsIni);
        var taskFilePath = workspace.GetPath("Task2024.txt");
        var tmpJsonPath = ProjectFiles.TmpJson(adminData);
        var runId = "run-20260322_161500_123";
        var revitMajor = 2024;

        var transferStore = new TransferStore();
        var model = TestModelFactory.Create(
            workspace.GetPath("Model.rvt"),
            new DateTime(2026, 3, 22, 16, 15, 0),
            workspace.CreateDirectory("out-map"),
            null);

        var envelope = transferStore.BuildEnvelope(revitMajor, runId, [model]);
        transferStore.WriteEnvelope(tmpJsonPath, envelope);
        transferStore.WriteTaskModels(taskFilePath, [workspace.GetPath("Other.rvt")]);

        using var env = CreateBatchEnvironment(adminData, taskFilePath, iniPath, runId, revitMajor);

        var context = CreateContext(adminData, taskFilePath, iniPath, runId, revitMajor);
        var sut = new BatchRunInputLoader(transferStore);

        var ok = sut.TryLoad(context, out var input);

        Assert.False(ok);
        Assert.Null(input);
        Assert.Equal(
            BatchRunStatuses.Failed,
            AddinRunStatusReader.TryReadStatus(
                ProjectFiles.AddinStatusFile(adminData),
                runId,
                Path.GetFileName(taskFilePath)));
    }

    private static BatchRunContext CreateContext(
        string adminData,
        string taskFilePath,
        string iniPath,
        string runId,
        int revitMajor)
    {
        return new BatchRunContext(
            adminData,
            taskFilePath,
            iniPath,
            runId,
            revitMajor,
            ProjectFiles.TmpJson(adminData),
            exportViewName: "Navisworks",
            enableUnmappedExport: false);
    }

    private static EnvironmentVariableScope CreateBatchEnvironment(
        string adminData,
        string taskFilePath,
        string iniPath,
        string runId,
        int revitMajor)
    {
        return new EnvironmentVariableScope(
            (EnvironmentVariableNames.AdminData, adminData),
            (EnvironmentVariableNames.TaskFile, taskFilePath),
            (EnvironmentVariableNames.SettingsIni, iniPath),
            (EnvironmentVariableNames.RunId, runId),
            (EnvironmentVariableNames.RevitMajor, revitMajor.ToString()));
    }
}
