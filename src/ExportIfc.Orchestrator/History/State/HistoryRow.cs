namespace ExportIfc.History;

/// <summary>
/// Одна запись рабочей истории состояния модели.
/// </summary>
/// <param name="Path">Нормализованный путь к RVT.</param>
/// <param name="LastModifiedMinute">
/// Дата модификации модели, нормализованная до минут.
/// </param>
/// <remarks>
/// Назначение:
/// Хранит минимальный доменный снимок состояния модели,
/// достаточный для проверки актуальности экспорта.
///
/// Контракты:
/// 1. <paramref name="Path"/> должен быть уже нормализован вызывающим кодом.
/// 2. <paramref name="LastModifiedMinute"/> должен храниться без секунд.
/// 3. Запись не содержит логики чтения, сравнения или сохранения истории.
/// </remarks>
internal readonly record struct HistoryRow(
    string Path,
    DateTime LastModifiedMinute);