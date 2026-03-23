using ExportIfc.Config;

namespace ExportIfc.RevitAddin.Startup;

/// <summary>
/// Определяет, должен ли add-in запускать batch-autorun внутри Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Читает env-флаг batch-запуска, переданный оркестратором.
/// 2. Отделяет batch-autorun от обычного ручного запуска Revit.
///
/// Контракты:
/// 1. Возвращает <see langword="true"/> только при явно включённом флаге
///    <see cref="EnvironmentVariableNames.Autorun"/>.
/// 2. Не содержит orchestration-логики и только определяет факт batch-запуска.
/// </remarks>
internal static class BatchLaunchDetector
{
    /// <summary>
    /// Возвращает признак batch-запуска add-in.
    /// </summary>
    /// <returns>
    /// <see langword="true"/>, если оркестратор явно запросил batch-autorun;
    /// иначе — <see langword="false"/>.
    /// </returns>
    public static bool IsBatchLaunchRequested()
    {
        var autorun = Environment.GetEnvironmentVariable(EnvironmentVariableNames.Autorun);

        return string.Equals(
            autorun,
            EnvironmentVariableValues.AutorunEnabled,
            StringComparison.Ordinal);
    }
}