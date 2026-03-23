using ExportIfc.Models;

namespace ExportIfc.Export.Planning;

/// <summary>
/// Один пакет моделей для запуска в определённой версии Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Хранит целевую major-версию Revit для запуска пакета.
/// 2. Хранит набор моделей, которые должны быть обработаны в этом запуске.
///
/// Контракты:
/// 1. Все модели внутри пакета относятся к одному запуску Revit
///    с major-версией <see cref="RevitMajor"/>.
/// 2. <see cref="Models"/> представляет готовый состав пакета
///    и доступен только для чтения через интерфейс списка.
/// 3. Класс не содержит логики построения пакета
///    и служит контейнером результата планирования.
/// </remarks>
internal sealed class RevitBatchPlanItem
{
    /// <summary>
    /// Создаёт пакет моделей для одной версии Revit.
    /// </summary>
    /// <param name="revitMajor">Целевая major-версия Revit.</param>
    /// <param name="models">Модели, входящие в пакет.</param>
    public RevitBatchPlanItem(
        int revitMajor,
        IReadOnlyList<RevitModel> models)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revitMajor, nameof(revitMajor));
        ArgumentNullException.ThrowIfNull(models);

        RevitMajor = revitMajor;
        Models = models;
    }

    /// <summary>
    /// Целевая major-версия Revit.
    /// </summary>
    public int RevitMajor { get; }

    /// <summary>
    /// Модели, входящие в пакет.
    /// </summary>
    public IReadOnlyList<RevitModel> Models { get; }
}