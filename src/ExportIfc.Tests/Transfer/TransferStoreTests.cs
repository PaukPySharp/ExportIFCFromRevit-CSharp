using ExportIfc.Config;
using ExportIfc.Models;
using ExportIfc.Tests.TestInfrastructure;
using ExportIfc.Transfer;

using Xunit;

namespace ExportIfc.Tests.Transfer;

/// <summary>
/// Проверяет transport-контракт <c>tmp.json</c>, task-файлов и их взаимную согласованность.
/// </summary>
/// <remarks>
/// Сьют закрывает не только сериализацию transport-пакета, но и более тонкий контракт,
/// при котором отключённый no-map маршрут обнуляет только каталог выгрузки,
/// а связанные конфигурационные значения модели могут оставаться заполненными.
/// </remarks>
public sealed class TransferStoreTests
{
    [Fact]
    public void EnvelopeAndTaskFiles_RoundTripSuccessfully()
    {
        using var workspace = TestWorkspace.Create();

        var dt = new DateTime(2026, 3, 8, 10, 0, 0);

        var model1 = TestModelFactory.Create(
            workspace.GetPath("A.rvt"),
            dt,
            workspace.GetPath("out-map"),
            workspace.GetPath("out-nomap"));

        var model2 = TestModelFactory.Create(
            workspace.GetPath("B.rvt"),
            dt,
            workspace.GetPath("out-map-2"),
            null);

        var sut = new TransferStore();

        var envelope = sut.BuildEnvelope(2024, "run-20260308_100000_123", [model1, model2]);

        var tmpJsonPath = workspace.GetPath("tmp.json");
        sut.WriteEnvelope(tmpJsonPath, envelope);

        var ok = sut.TryReadEnvelope(tmpJsonPath, out var restored);

        Assert.True(ok);
        Assert.NotNull(restored);
        Assert.Equal("run-20260308_100000_123", restored!.RunId);
        Assert.Equal(2024, restored!.RevitMajor);
        Assert.Equal(2, restored.Items.Count);
        Assert.Equal(model1.RvtPath, restored.Items[0].ModelPath);
        Assert.Equal(model2.RvtPath, restored.Items[1].ModelPath);

        var taskFilePath = workspace.GetPath("Task2024.txt");
        sut.WriteTaskModels(taskFilePath, [model1.RvtPath, model2.RvtPath]);

        var taskModels = sut.ReadTaskModels(taskFilePath);

        Assert.Equal([model1.RvtPath, model2.RvtPath], taskModels);
        Assert.Null(sut.DescribeTaskModelMismatch(restored, taskModels));
    }

    [Fact]
    public void BuildEnvelope_PreservesNoMapConfig_WhenOnlyOutputDirectoryIsDisabled()
    {
        var dt = new DateTime(2026, 3, 8, 10, 0, 0);
        var model = TestModelFactory.Create(
            @"C:\Models\A.rvt",
            dt,
            outputDirMapping: @"C:\Out\Map",
            outputDirNoMap: null,
            noMapJson: @"C:\Config\NoMap.json");

        var sut = new TransferStore();

        var envelope = sut.BuildEnvelope(2024, "run-20260308_100000_123", [model]);

        Assert.Single(envelope.Items);
        Assert.Null(envelope.Items[0].OutputDirNoMap);
        Assert.Equal(@"C:\Config\NoMap.json", envelope.Items[0].NoMapJson);
    }

    [Fact]
    public void BuildEnvelope_Throws_WhenRevitMajorIsNotPositive()
    {
        var dt = new DateTime(2026, 3, 8, 10, 0, 0);
        var model = TestModelFactory.Create(@"C:\Models\A.rvt", dt);
        var sut = new TransferStore();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => sut.BuildEnvelope(0, "run-20260308_100000_123", [model]));

        Assert.Equal("revitMajor", ex.ParamName);
    }

    [Fact]
    public void BuildEnvelope_Throws_WhenRunIdIsBlank()
    {
        var dt = new DateTime(2026, 3, 8, 10, 0, 0);
        var model = TestModelFactory.Create(@"C:\Models\A.rvt", dt);
        var sut = new TransferStore();

        var ex = Assert.Throws<ArgumentException>(
            () => sut.BuildEnvelope(2024, " ", [model]));

        Assert.Equal("runId", ex.ParamName);
        Assert.Contains("RunId", ex.Message);
    }

    [Fact]
    public void BuildEnvelope_Throws_WhenModelPathIsBlank()
    {
        var sut = new TransferStore();

        var invalidModel = new RevitModel
        {
            RvtPath = " ",
            LastModifiedMinute = new DateTime(2026, 3, 8, 10, 0, 0),
            OutputDirMapping = @"C:\Out\Map",
            MappingJson = @"C:\Config\mapping.json",
            IfcClassMappingFile = @"C:\Config\family-mapping.txt"
        };

        var ex = Assert.Throws<ArgumentException>(
            () => sut.BuildEnvelope(2024, "run-20260308_100000_123", [invalidModel]));

        Assert.Equal("models", ex.ParamName);
        Assert.Contains("непустой путь к RVT", ex.Message);
    }

    [Fact]
    public void TryReadEnvelope_ReturnsFalse_ForStructurallyInvalidPayload()
    {
        using var workspace = TestWorkspace.Create();

        var tmpJsonPath = workspace.GetPath("tmp.json");
        File.WriteAllText(
            tmpJsonPath,
            """
            {
              "RunId": "run-20260308_100000_123",
              "RevitMajor": 2024,
              "Items": [
                {
                  "ModelPath": ""
                }
              ]
            }
            """,
            ProjectEncodings.Utf8NoBom);

        var sut = new TransferStore();

        var ok = sut.TryReadEnvelope(tmpJsonPath, out var envelope);

        Assert.False(ok);
        Assert.Null(envelope);
    }

    [Fact]
    public void DescribeTaskModelMismatch_ReturnsMessage_WhenCountsDiffer()
    {
        var dt = new DateTime(2026, 3, 8, 10, 0, 0);
        var model1 = TestModelFactory.Create(@"C:\Models\A.rvt", dt);
        var model2 = TestModelFactory.Create(@"C:\Models\B.rvt", dt);

        var sut = new TransferStore();
        var envelope = sut.BuildEnvelope(2024, "run-20260308_100000_123", [model1, model2]);

        var message = sut.DescribeTaskModelMismatch(envelope, [model1.RvtPath]);

        Assert.NotNull(message);
        Assert.Contains("Количество моделей не совпадает", message);
    }

    [Fact]
    public void DescribeTaskModelMismatch_ReturnsMessage_WhenOrderDiffers()
    {
        var dt = new DateTime(2026, 3, 8, 10, 0, 0);
        var model1 = TestModelFactory.Create(@"C:\Models\A.rvt", dt);
        var model2 = TestModelFactory.Create(@"C:\Models\B.rvt", dt);

        var sut = new TransferStore();
        var envelope = sut.BuildEnvelope(2024, "run-20260308_100000_123", [model1, model2]);

        var message = sut.DescribeTaskModelMismatch(envelope, [model2.RvtPath, model1.RvtPath]);

        Assert.NotNull(message);
        Assert.Contains("Расхождение в позиции 1", message);
    }
}
