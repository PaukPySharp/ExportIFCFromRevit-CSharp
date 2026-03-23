namespace ExportIfc.IO;

/// <summary>
/// Общие хелперы для путей, имён файлов и простых операций
/// над файловой системой.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует повторяющуюся логику нормализации путей.
/// 2. Даёт общие проверки для значений из Excel и настроек.
/// 3. Собирает в одном месте работу с расширениями и безопасными именами файлов.
///
/// Контракты:
/// 1. Методы класса не создают файлов и каталогов, если это явно не требуется.
/// 2. Методы вида Try* не выбрасывают исключения при невалидном вводе.
/// 3. Нормализация пути не гарантирует существование файла или каталога.
/// 4. Проверка абсолютного пути опирается на исходную строку,
///    а не на результат приведения через <see cref="Path.GetFullPath(string)"/>.
/// </remarks>
public static partial class FileSystemEx
{
    /// <summary>
    /// Зарезервированные имена файлов Windows, которые нельзя использовать как имя.
    /// </summary>
    private static readonly HashSet<string> _windowsReservedFileNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

    /// <summary>
    /// Приводит расширение к виду с ведущей точкой.
    /// </summary>
    /// <param name="extension">Исходное расширение.</param>
    /// <returns>Нормализованное расширение либо пустая строка.</returns>
    private static string NormalizeExtension(string extension)
    {
        var trimmed = (extension ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        return trimmed[0] == '.'
            ? trimmed
            : "." + trimmed;
    }

    /// <summary>
    /// Проверяет, что путь уже задан как полностью определённый абсолютный.
    /// </summary>
    /// <param name="path">Путь для проверки.</param>
    /// <returns>
    /// <see langword="true"/>, если путь можно использовать как абсолютный без
    /// дополнительной привязки к текущему каталогу или диску; иначе <see langword="false"/>.
    /// </returns>
    private static bool IsFullyQualifiedAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (!Path.IsPathRooted(path))
            return false;

        if (path.StartsWith(@"\\", StringComparison.Ordinal))
            return true;

        if (path.Length >= 3
            && char.IsLetter(path[0])
            && path[1] == ':'
            && IsDirectorySeparator(path[2]))
        {
            return true;
        }

        return Path.DirectorySeparatorChar == '/' && path[0] == '/';
    }

    /// <summary>
    /// Проверяет, что символ является разделителем каталога.
    /// </summary>
    /// <param name="value">Проверяемый символ.</param>
    /// <returns><see langword="true"/>, если символ является разделителем каталога.</returns>
    private static bool IsDirectorySeparator(char value)
    {
        return value == '\\' || value == '/';
    }

    /// <summary>
    /// Убирает недопустимые завершающие точки и пробелы из имени файла.
    /// </summary>
    /// <param name="value">Кандидат имени файла.</param>
    /// <returns>
    /// Исправленное значение. Если после обрезки имя становится пустым,
    /// возвращается подчёркивание.
    /// </returns>
    private static string ReplaceTrailingDotsAndSpaces(string value)
    {
        var trimmed = value.TrimEnd('.', ' ');

        if (trimmed.Length == value.Length)
            return value;

        return trimmed.Length == 0
            ? "_"
            : trimmed + "_";
    }

    /// <summary>
    /// Защищает от зарезервированных имён Windows.
    /// </summary>
    /// <param name="value">Кандидат имени файла.</param>
    /// <returns>Безопасное имя файла.</returns>
    private static string ProtectReservedWindowsFileNames(string value)
    {
        if (value.Length == 0)
            return value;

        var stem = Path.GetFileNameWithoutExtension(value);
        if (!_windowsReservedFileNames.Contains(stem))
            return value;

        var extension = Path.GetExtension(value);
        return stem + "_" + extension;
    }

    /// <summary>
    /// Проверяет, что имя заканчивается недопустимой точкой или пробелом.
    /// </summary>
    /// <param name="value">Проверяемое имя.</param>
    /// <returns>
    /// <see langword="true"/>, если имя заканчивается точкой или пробелом;
    /// иначе <see langword="false"/>.
    /// </returns>
    private static bool EndsWithDotOrSpace(string value)
    {
        if (value.Length == 0)
            return false;

        var lastChar = value[value.Length - 1];
        return lastChar == '.' || lastChar == ' ';
    }
}
