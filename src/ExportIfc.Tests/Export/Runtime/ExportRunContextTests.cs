using System.Text.RegularExpressions;

using ExportIfc.Export.Runtime;
using ExportIfc.Tests.TestInfrastructure;

using Xunit;

namespace ExportIfc.Tests.Export.Runtime;

/// <summary>
/// Проверяет формат runtime-идентификатора запуска оркестратора.
/// </summary>
public sealed class ExportRunContextTests
{
    [Fact]
    public void Create_UsesRunIdWithMillisecondPrecision()
    {
        using var workspace = TestWorkspace.Create();

        var settings = TestAppSettingsFactory.Create(
            workspace.Root,
            dirExportConfig: workspace.CreateDirectory("export-config"),
            dirAdminData: workspace.CreateDirectory("admin_data"));

        var context = ExportRunContext.Create(settings);

        Assert.Matches(
            new Regex(@"^\d{8}_\d{6}_\d{3}$", RegexOptions.CultureInvariant),
            context.RunId);
    }
}
