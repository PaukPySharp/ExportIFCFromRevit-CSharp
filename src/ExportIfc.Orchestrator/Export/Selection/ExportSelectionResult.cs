using ExportIfc.Models;

namespace ExportIfc.Export.Selection;

/// <summary>
/// Результат проверки актуальности моделей перед экспортом.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Возвращает итоговый набор моделей, дошедших до batch-планирования.
/// 2. Отдельно хранит число моделей, пропущенных как уже актуальные.
/// 3. Не смешивает состав набора на выгрузку со статистикой отбора.
///
/// Контракты:
/// 1. <see cref="ModelsToExport"/> содержит только модели, реально требующие дальнейшей обработки.
/// 2. <see cref="SkippedAsActual"/> не может быть отрицательным.
/// 3. Класс хранит уже готовый результат отбора и не содержит прикладной логики.
/// </remarks>
internal sealed class ExportSelectionResult
{
    /// <summary>
    /// Создаёт результат проверки актуальности.
    /// </summary>
    /// <param name="modelsToExport">Модели, которые нужно выгружать.</param>
    /// <param name="skippedAsActual">Количество пропущенных актуальных моделей.</param>
    public ExportSelectionResult(
        IReadOnlyList<RevitModel> modelsToExport,
        int skippedAsActual)
    {
        ArgumentNullException.ThrowIfNull(modelsToExport);
        ArgumentOutOfRangeException.ThrowIfNegative(skippedAsActual, nameof(skippedAsActual));

        ModelsToExport = modelsToExport;
        SkippedAsActual = skippedAsActual;
    }

    /// <summary>
    /// Модели, которые должны быть переданы в экспорт.
    /// </summary>
    public IReadOnlyList<RevitModel> ModelsToExport { get; }

    /// <summary>
    /// Количество моделей, пропущенных как уже актуальные.
    /// </summary>
    public int SkippedAsActual { get; }
}