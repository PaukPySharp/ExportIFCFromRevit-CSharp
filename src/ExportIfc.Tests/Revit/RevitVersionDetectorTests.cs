using System.Text;

using ExportIfc.Revit;
using ExportIfc.Tests.TestInfrastructure;

using Xunit;

namespace ExportIfc.Tests.Revit;

/// <summary>
/// Проверяет распознавание версии RVT-файла, когда маркеры не попадают в быстрый префикс.
/// </summary>
public sealed class RevitVersionDetectorTests
{
    [Fact]
    public void TryGetInfo_ReadsMarkersBeyondFastPrefix()
    {
        using var workspace = TestWorkspace.Create();

        var rvtPath = workspace.GetPath("late-version-marker.rvt");
        var payload = BuildLateMarkerPayload(
            "Format: 2025",
            "Build: 25.0.1.123)");

        File.WriteAllBytes(rvtPath, payload);

        var info = RevitVersionDetector.TryGetInfo(rvtPath);

        Assert.NotNull(info);
        Assert.Equal(2025, info!.Year);
        Assert.Equal("25.0.1.123", info.Build);
    }

    [Fact]
    public void TryGetRevitMajor_ReadsYearWhenMarkerLiesOutsideFastPrefix()
    {
        using var workspace = TestWorkspace.Create();

        var rvtPath = workspace.GetPath("late-major-marker.rvt");
        var payload = BuildLateMarkerPayload(
            "Autodesk Revit 2024",
            marker2: null);

        File.WriteAllBytes(rvtPath, payload);

        var major = RevitVersionDetector.TryGetRevitMajor(rvtPath);

        Assert.Equal(2024, major);
    }

    private static byte[] BuildLateMarkerPayload(string marker1, string? marker2)
    {
        var filler = new byte[(128 * 1024) + 1024];
        var marker1Bytes = Encoding.Unicode.GetBytes(marker1);
        var separatorBytes = marker2 is null
            ? Array.Empty<byte>()
            : Encoding.Unicode.GetBytes("\n");
        var marker2Bytes = marker2 is null
            ? Array.Empty<byte>()
            : Encoding.Unicode.GetBytes(marker2);

        var payload = new byte[
            filler.Length +
            marker1Bytes.Length +
            separatorBytes.Length +
            marker2Bytes.Length];
        Buffer.BlockCopy(filler, 0, payload, 0, filler.Length);
        Buffer.BlockCopy(marker1Bytes, 0, payload, filler.Length, marker1Bytes.Length);
        Buffer.BlockCopy(
            separatorBytes,
            0,
            payload,
            filler.Length + marker1Bytes.Length,
            separatorBytes.Length);
        Buffer.BlockCopy(
            marker2Bytes,
            0,
            payload,
            filler.Length + marker1Bytes.Length + separatorBytes.Length,
            marker2Bytes.Length);

        return payload;
    }
}
