using ExportIfc.Config;
using ExportIfc.Tests.TestInfrastructure;
using ExportIfc.Transfer;

using Xunit;

namespace ExportIfc.Tests.Transfer;

/// <summary>
/// Проверяет чтение статуса add-in по паре <c>RunId</c> и <c>TaskFile</c>.
/// </summary>
public sealed class AddinRunStatusReaderTests
{
    [Fact]
    public void TryReadStatus_ReturnsStatus_ForMatchingRunIdAndTaskFile()
    {
        using var workspace = TestWorkspace.Create();
        var statusFilePath = workspace.GetPath("addin-status.txt");

        File.WriteAllLines(
            statusFilePath,
            [
                "RunId=run-1",
                "RevitMajor=2024",
                "TaskFile=Task2024.txt",
                "Status=Ok",
                "RunId=run-1",
                "RevitMajor=2025",
                "TaskFile=Task2025.txt",
                "Status=Failed"
            ],
            ProjectEncodings.Utf8NoBom);

        var status2024 = AddinRunStatusReader.TryReadStatus(statusFilePath, "run-1", "Task2024.txt");
        var status2025 = AddinRunStatusReader.TryReadStatus(statusFilePath, "run-1", "Task2025.txt");

        Assert.Equal("Ok", status2024);
        Assert.Equal("Failed", status2025);
    }

    [Fact]
    public void TryReadStatus_ReturnsLastStatus_ForRepeatedBlockOfSameRunAndTask()
    {
        using var workspace = TestWorkspace.Create();
        var statusFilePath = workspace.GetPath("addin-status.txt");

        // Один и тот же batch может оставить в журнале несколько Status-строк.
        // Reader обязан брать последнюю для конкретной пары RunId/TaskFile.
        File.WriteAllLines(
            statusFilePath,
            [
                "RunId=run-1",
                "TaskFile=Task2024.txt",
                "Status=Started",
                "Status=Ok"
            ],
            ProjectEncodings.Utf8NoBom);

        var status = AddinRunStatusReader.TryReadStatus(statusFilePath, "run-1", "Task2024.txt");

        Assert.Equal("Ok", status);
    }

    [Fact]
    public void TryReadStatus_ReturnsNull_WhenOnlyAnotherTaskFileOfSameRunHasStatus()
    {
        using var workspace = TestWorkspace.Create();
        var statusFilePath = workspace.GetPath("addin-status.txt");

        File.WriteAllLines(
            statusFilePath,
            [
                "RunId=run-1",
                "RevitMajor=2024",
                "TaskFile=Task2024.txt",
                "Status=Ok"
            ],
            ProjectEncodings.Utf8NoBom);

        var status = AddinRunStatusReader.TryReadStatus(statusFilePath, "run-1", "Task2025.txt");

        Assert.Null(status);
    }
}
