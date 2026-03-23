namespace ExportIfc.RevitAddin.Batch.Export.Execution;

/// <summary>
/// Итог одной попытки экспорта модели или отдельного маршрута.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Хранить агрегированный результат одной попытки экспорта.
/// 2. Не размазывать по коду набор связанных bool-флагов.
///
/// Контракты:
/// 1. <see cref="Succeeded"/> отражает общий итог попытки.
/// 2. <see cref="HasExports"/> показывает, что хотя бы один маршрут реально запускался.
/// 3. <see cref="HasOnlySuspiciousSmallIfc"/> истинно только для успешной попытки,
///    в которой все успешные IFC оказались подозрительно маленькими.
/// </remarks>
internal readonly struct ExportAttemptResult
{
    private ExportAttemptResult(
        bool succeeded,
        bool hasExports,
        bool hasSuspiciousSmallIfc,
        bool hasNormalSizedIfc)
    {
        Succeeded = succeeded;
        HasExports = hasExports;
        HasSuspiciousSmallIfc = hasSuspiciousSmallIfc;
        HasNormalSizedIfc = hasNormalSizedIfc;
    }

    /// <summary>
    /// Получает признак успешности попытки экспорта.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Получает признак того, что хотя бы один маршрут экспорта реально выполнялся.
    /// </summary>
    public bool HasExports { get; }

    /// <summary>
    /// Получает признак наличия подозрительно маленького IFC среди успешных маршрутов.
    /// </summary>
    public bool HasSuspiciousSmallIfc { get; }

    /// <summary>
    /// Получает признак наличия хотя бы одного нормального по размеру IFC среди успешных маршрутов.
    /// </summary>
    public bool HasNormalSizedIfc { get; }

    /// <summary>
    /// Получает признак того, что все успешные IFC в попытке оказались подозрительно маленькими.
    /// </summary>
    public bool HasOnlySuspiciousSmallIfc =>
        Succeeded &&
        HasExports &&
        HasSuspiciousSmallIfc &&
        !HasNormalSizedIfc;

    /// <summary>
    /// Создаёт начальное состояние для агрегации маршрутов экспорта.
    /// </summary>
    public static ExportAttemptResult Start()
        => new(true, false, false, false);

    /// <summary>
    /// Создаёт результат успешного маршрута с нормальным по размеру IFC.
    /// </summary>
    public static ExportAttemptResult SuccessfulNormal()
        => new(true, true, false, true);

    /// <summary>
    /// Создаёт результат успешного маршрута с подозрительно маленьким IFC.
    /// </summary>
    public static ExportAttemptResult SuccessfulSuspicious()
        => new(true, true, true, false);

    /// <summary>
    /// Создаёт результат неуспешной попытки после фактического запуска экспорта.
    /// </summary>
    public static ExportAttemptResult FailedWithExports()
        => new(false, true, false, false);

    /// <summary>
    /// Создаёт результат неуспешной попытки без фактического запуска маршрутов экспорта.
    /// </summary>
    public static ExportAttemptResult FailedWithoutExports()
        => new(false, false, false, false);

    /// <summary>
    /// Объединяет результаты нескольких маршрутов в одну попытку экспорта модели.
    /// </summary>
    /// <param name="other">Результат очередного маршрута экспорта.</param>
    /// <returns>Агрегированный результат попытки.</returns>
    public ExportAttemptResult Merge(ExportAttemptResult other)
    {
        return new ExportAttemptResult(
            Succeeded && other.Succeeded,
            HasExports || other.HasExports,
            HasSuspiciousSmallIfc || other.HasSuspiciousSmallIfc,
            HasNormalSizedIfc || other.HasNormalSizedIfc);
    }
}