using ExportIfc.Config;
using ExportIfc.Export.Planning;
using ExportIfc.Export.Runtime;
using ExportIfc.Revit;
using ExportIfc.Tests.TestInfrastructure;
using ExportIfc.Transfer;

using Xunit;

namespace ExportIfc.Tests.Export.Runtime;

/// <summary>
/// Проверяет dry-run и real-run контракт пакетного запуска Revit.
/// </summary>
/// <remarks>
/// Сьют фиксирует две ключевые договорённости orchestration-слоя:
/// dry-run готовит transport-артефакты без запуска внешнего процесса,
/// а real-run перезаписывает общий <c>tmp.json</c> перед каждым batch-пакетом и продолжает работу после неуспешного запуска.
/// </remarks>
public sealed class RevitBatchRunnerTests
{
    [Fact]
    public void Execute_DryRun_WritesTaskFilesAndPerVersionJson_WithoutLaunchingRevit()
    {
        using var workspace = TestWorkspace.Create();

        var context = CreateContext(workspace, runRevit: false);
        var launcher = new FakeRevitLauncher();
        var transferStore = new TransferStore();
        var sut = new RevitBatchRunner(launcher, transferStore);

        var dt = new DateTime(2026, 3, 8, 10, 0, 0);

        var model2022 = TestModelFactory.Create(
            workspace.GetPath("A.rvt"),
            dt,
            workspace.CreateDirectory("out-map-2022"),
            null);

        var model2023 = TestModelFactory.Create(
            workspace.GetPath("B.rvt"),
            dt,
            workspace.CreateDirectory("out-map-2023"),
            workspace.CreateDirectory("out-nomap-2023"));

        var batchPlan = new RevitBatchPlan(
            [
                new RevitBatchPlanItem(2022, [model2022]),
                new RevitBatchPlanItem(2023, [model2023])
            ],
            Array.Empty<string>(),
            Array.Empty<string>());

        var hasFailures = sut.Execute(context, batchPlan);

        Assert.False(hasFailures);
        Assert.Empty(launcher.Calls);
        Assert.False(File.Exists(context.TmpJsonPath));

        Assert.Equal(
            [model2022.RvtPath],
            transferStore.ReadTaskModels(ProjectFiles.TaskFile(context.Paths, 2022)));

        Assert.Equal(
            [model2023.RvtPath],
            transferStore.ReadTaskModels(ProjectFiles.TaskFile(context.Paths, 2023)));

        Assert.True(transferStore.TryReadEnvelope(
            ProjectFiles.DryRunTransferJson(context.Paths, 2022),
            out var envelope2022));

        Assert.NotNull(envelope2022);
        Assert.Equal(context.RunId, envelope2022!.RunId);
        Assert.Equal(2022, envelope2022!.RevitMajor);
        Assert.Single(envelope2022.Items);
        Assert.Equal(model2022.RvtPath, envelope2022.Items[0].ModelPath);

        Assert.True(transferStore.TryReadEnvelope(
            ProjectFiles.DryRunTransferJson(context.Paths, 2023),
            out var envelope2023));

        Assert.NotNull(envelope2023);
        Assert.Equal(context.RunId, envelope2023!.RunId);
        Assert.Equal(2023, envelope2023!.RevitMajor);
        Assert.Single(envelope2023.Items);
        Assert.Equal(model2023.RvtPath, envelope2023.Items[0].ModelPath);
    }

    [Fact]
    public void Execute_ContinuesAfterFailedBatch_AndOverwritesSharedTmpJson()
    {
        using var workspace = TestWorkspace.Create();

        var context = CreateContext(
            workspace,
            runRevit: true,
            revitBatchTimeoutMinutes: 15);

        var launcher = new FakeRevitLauncher(false, true);
        var transferStore = new TransferStore();
        var sut = new RevitBatchRunner(launcher, transferStore);

        var dt = new DateTime(2026, 3, 8, 10, 0, 0);

        var model2022 = TestModelFactory.Create(
            workspace.GetPath("A.rvt"),
            dt,
            workspace.CreateDirectory("out-map-2022"),
            null);

        var model2023 = TestModelFactory.Create(
            workspace.GetPath("B.rvt"),
            dt,
            workspace.CreateDirectory("out-map-2023"),
            null);

        var batchPlan = new RevitBatchPlan(
            [
                new RevitBatchPlanItem(2022, [model2022]),
                new RevitBatchPlanItem(2023, [model2023])
            ],
            Array.Empty<string>(),
            Array.Empty<string>());

        var hasFailures = sut.Execute(context, batchPlan);

        Assert.True(hasFailures);
        Assert.Equal(2, launcher.Calls.Count);

        Assert.Collection(
            launcher.Calls,
            call =>
            {
                Assert.Equal(2022, call.RevitMajor);
                Assert.Equal(ProjectFiles.TaskFile(context.Paths, 2022), call.TaskFilePath);
                Assert.Equal(context.Paths.DirAdminData, call.DirAdminData);
                Assert.Equal(context.RunId, call.RunId);
                Assert.Equal(15, call.TimeoutMinutes);
            },
            call =>
            {
                Assert.Equal(2023, call.RevitMajor);
                Assert.Equal(ProjectFiles.TaskFile(context.Paths, 2023), call.TaskFilePath);
                Assert.Equal(context.Paths.DirAdminData, call.DirAdminData);
                Assert.Equal(context.RunId, call.RunId);
                Assert.Equal(15, call.TimeoutMinutes);
            });

        Assert.True(File.Exists(context.TmpJsonPath));
        Assert.True(transferStore.TryReadEnvelope(context.TmpJsonPath, out var lastEnvelope));

        // Общий tmp.json должен отражать последний реально подготовленный пакет,
        // а не состояние первого запуска или dry-run артефакт из другой версии Revit.
        Assert.NotNull(lastEnvelope);
        Assert.Equal(context.RunId, lastEnvelope!.RunId);
        Assert.Equal(2023, lastEnvelope!.RevitMajor);
        Assert.Single(lastEnvelope.Items);
        Assert.Equal(model2023.RvtPath, lastEnvelope.Items[0].ModelPath);
    }

    /// <summary>
    /// Создаёт runtime-контекст экспорта поверх временного тестового окружения.
    /// </summary>
    /// <param name="workspace">Рабочее пространство теста.</param>
    /// <param name="runRevit">Признак запуска внешнего Revit.</param>
    /// <param name="revitBatchTimeoutMinutes">Таймаут ожидания batch-процесса Revit.</param>
    /// <returns>Подготовленный runtime-контекст экспорта.</returns>
    private static ExportRunContext CreateContext(
        TestWorkspace workspace,
        bool runRevit,
        int revitBatchTimeoutMinutes = 0)
    {
        var exportConfig = workspace.CreateDirectory("export-config");
        var adminData = workspace.CreateDirectory("admin_data");

        var settings = TestAppSettingsFactory.Create(
            workspace.Root,
            dirExportConfig: exportConfig,
            dirAdminData: adminData,
            runRevit: runRevit,
            revitBatchTimeoutMinutes: revitBatchTimeoutMinutes);

        return ExportRunContext.Create(settings);
    }

    /// <summary>
    /// Test-double реализация <see cref="IRevitLauncher"/>, фиксирующая фактические вызовы.
    /// </summary>
    private sealed class FakeRevitLauncher : IRevitLauncher
    {
        private readonly Queue<bool> _results;

        public FakeRevitLauncher(params bool[] results)
        {
            _results = new Queue<bool>(results);
        }

        /// <summary>
        /// Получает журнал вызовов launcher'а в порядке исполнения batch-пакетов.
        /// </summary>
        public List<Call> Calls { get; } = [];

        public bool RunAndWait(
            int revitMajor,
            string taskFilePath,
            string dirAdminData,
            string iniPath,
            string runId,
            int timeoutMinutes)
        {
            Calls.Add(new Call(
                revitMajor,
                taskFilePath,
                dirAdminData,
                iniPath,
                runId,
                timeoutMinutes));

            return _results.Count > 0
                ? _results.Dequeue()
                : true;
        }

        /// <summary>
        /// Снимок одного вызова <see cref="RunAndWait"/>.
        /// </summary>
        public sealed record Call(
            int RevitMajor,
            string TaskFilePath,
            string DirAdminData,
            string IniPath,
            string RunId,
            int TimeoutMinutes);
    }
}
