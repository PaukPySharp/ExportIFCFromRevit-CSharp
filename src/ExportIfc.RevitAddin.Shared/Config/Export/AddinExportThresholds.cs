namespace ExportIfc.RevitAddin.Config.Export;

/// <summary>
/// Диагностические пороги add-in экспорта.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует tweakable-пороги экспортного пайплайна.
/// 2. Убирает магические числа из batch-исполнителей.
///
/// Контракты:
/// 1. Значения класса относятся только к add-in экспортному пайплайну.
/// 2. Класс не хранит пользовательских текстов и не смешивает разные домены.
/// </remarks>
internal static class AddinExportThresholds
{
    /// <summary>
    /// Размер IFC, ниже которого результат считается подозрительно маленьким.
    /// </summary>
    public const long SuspiciousSmallIfcBytes = 10 * 1024;
}