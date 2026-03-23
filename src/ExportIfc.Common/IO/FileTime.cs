namespace ExportIfc.IO;

/// <summary>
/// Утилиты для чтения и нормализации времени модификации файлов.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Предоставляет безопасные хелперы для чтения времени изменения файлов
///    без выброса исключений наружу в сценариях массовой обработки моделей.
/// 2. Централизует нормализацию времени до точности минуты,
///    чтобы сравнения не шумели из-за секунд.
///
/// Контракты:
/// 1. Методы класса не создают и не изменяют файлы.
/// 2. Если время модификации не удалось получить, вызывающий код получает
///    <see langword="null"/> и сам решает, как обработать ситуацию.
/// 3. Возвращаемое и нормализуемое время приводится к точности минуты.
/// </remarks>
public static class FileTime
{
    /// <summary>
    /// Возвращает время последнего изменения файла с точностью до минуты.
    /// </summary>
    /// <param name="path">Путь к файлу.</param>
    /// <returns>
    /// Время изменения без секунд либо <see langword="null"/>,
    /// если значение не удалось получить.
    /// </returns>
    /// <remarks>
    /// Метод предназначен для сценариев сравнения RVT и IFC по времени изменения,
    /// где секундная точность не нужна и только добавляет лишний шум.
    /// </remarks>
    public static DateTime? GetMTimeMinute(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return NormalizeMinute(File.GetLastWriteTime(path));
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Нормализует дату и время до точности в одну минуту.
    /// </summary>
    /// <param name="value">Исходное значение времени.</param>
    /// <returns>Значение без секунд.</returns>
    public static DateTime NormalizeMinute(DateTime value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);
}