namespace ExportIfc.Export.Planning;

/// <summary>
/// План пакетной обработки моделей по версиям Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Хранит итоговые пакеты моделей для запуска по версиям Revit.
/// 2. Хранит диагностические списки моделей, которые не попали в пакетный запуск.
///
/// Контракты:
/// 1. <see cref="Batches"/> содержит только готовые пакеты на запуск.
/// 2. <see cref="VersionNotFound"/> содержит пути моделей,
///    для которых не удалось определить версию Revit.
/// 3. <see cref="VersionTooNew"/> содержит диагностические сообщения
///    по моделям с версией выше всех доступных версий запуска Revit.
/// 4. Класс хранит готовый результат планирования
///    и не содержит прикладной логики маршрутизации.
/// </remarks>
internal sealed class RevitBatchPlan
{
    /// <summary>
    /// Создаёт план пакетной обработки.
    /// </summary>
    /// <param name="batches">Пакеты моделей по версиям Revit.</param>
    /// <param name="versionNotFound">Модели, для которых не удалось определить версию Revit.</param>
    /// <param name="versionTooNew">Диагностические сообщения по моделям со слишком новой версией Revit.</param>
    public RevitBatchPlan(
        IReadOnlyList<RevitBatchPlanItem> batches,
        IReadOnlyList<string> versionNotFound,
        IReadOnlyList<string> versionTooNew)
    {
        ArgumentNullException.ThrowIfNull(batches);
        ArgumentNullException.ThrowIfNull(versionNotFound);
        ArgumentNullException.ThrowIfNull(versionTooNew);

        Batches = batches;
        VersionNotFound = versionNotFound;
        VersionTooNew = versionTooNew;
    }

    /// <summary>
    /// Пакеты моделей по версиям Revit.
    /// </summary>
    public IReadOnlyList<RevitBatchPlanItem> Batches { get; }

    /// <summary>
    /// Пути моделей, для которых не удалось определить версию Revit.
    /// </summary>
    public IReadOnlyList<string> VersionNotFound { get; }

    /// <summary>
    /// Диагностические сообщения по моделям
    /// с версией Revit выше всех доступных версий запуска.
    /// </summary>
    public IReadOnlyList<string> VersionTooNew { get; }

    /// <summary>
    /// Признак наличия хотя бы одного пакета на запуск.
    /// </summary>
    public bool HasBatches => Batches.Count > 0;
}
